using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators.Checkout;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Checkout;

public sealed class CheckoutService(
    ICheckoutRepository repository,
    IAdministrationRepository accounts,
    IUseCaseUnitOfWork unitOfWork,
    IActorContext actor,
    TimeProvider clock,
    CheckoutPolicy policy,
    IAddressCatalog addressCatalog,
    IGhtkShippingRateProvider shippingRates) : ICheckoutService
{
    public async Task<UseCaseResult<CheckoutContextDto>> GetContextAsync(
        CancellationToken cancellationToken)
    {
        if (!await IsRetailCustomerAsync(cancellationToken))
        {
            return Denied<CheckoutContextDto>();
        }
        var items = await repository.SelectedItemsAsync(actor.UserId, cancellationToken);
        var addresses = await repository.AddressesAsync(actor.UserId, cancellationToken);
        var vouchers = await repository.VouchersAsync(clock.GetUtcNow(), cancellationToken);
        var lines = items.Select(item => Line(item, [], RetailPricing.UnitPrice(item.Product,
            items.Where(x => x.ProductId == item.ProductId).Sum(x => x.Quantity)))).ToList();
        return UseCaseResult<CheckoutContextDto>.Success(new CheckoutContextDto(lines,
            addresses.Select(x => new CheckoutAddress(x.RecipientName, x.RecipientPhone,
                x.ProvinceCode ?? "", x.ProvinceCity, x.District ?? "", x.WardCode ?? "", x.Ward,
                x.AddressDetail, x.AddressCatalogVersion ?? "")).ToList(),
            await repository.PointsAsync(actor.UserId, cancellationToken),
            vouchers.Where(x => x.FarmerProfileId is null
                || lines.Any(line => line.FarmerId == x.FarmerProfileId))
                .Select(x => x.VoucherCode).ToList()));
    }

    public IReadOnlyList<DeliveryArea> GetAreas() => policy.Areas;

    public async Task<UseCaseResult<CheckoutQuoteDto>> QuoteAsync(CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsRetailCustomerAsync(cancellationToken))
        {
            return Denied<CheckoutQuoteDto>();
        }
        var calculated = await CalculateAsync(request, cancellationToken);
        if (!calculated.IsSuccess)
        {
            return calculated;
        }
        var quote = calculated.Value!;
        var json = JsonSerializer.Serialize(quote);
        repository.AddQuote(new RetailCheckoutQuote
        {
            QuoteId = quote.Id, BuyerUserId = actor.UserId, AcceptedSnapshotJson = json,
            Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
            CreatedAt = clock.GetUtcNow().UtcDateTime, ExpiresAt = quote.ExpiresAt.UtcDateTime
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return calculated;
    }

    public async Task<UseCaseResult<OrderDto>> PlaceAsync(PlaceOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Confirmed || request.QuoteId == Guid.Empty
            || !Guid.TryParseExact(request.RequestKey, "D", out _))
        {
            return UseCaseResult<OrderDto>.Failure("VALIDATION", "Cần xác nhận bản tính giá hợp lệ.");
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await IsRetailCustomerAsync(cancellationToken))
        {
            return Denied<OrderDto>();
        }
        var existing = await repository.ExistingOrderAsync(actor.UserId, request.RequestKey,
            request.QuoteId, cancellationToken);
        if (existing is not null)
        {
            if (existing.RetailQuoteId != request.QuoteId)
            {
                return UseCaseResult<OrderDto>.Failure("IDEMPOTENCY_CONFLICT",
                    "Mã yêu cầu đã được dùng cho bản tính giá khác.", "Conflict");
            }
            return UseCaseResult<OrderDto>.Success(ToOrderDto(existing, true));
        }
        var stored = await repository.QuoteAsync(request.QuoteId, actor.UserId, cancellationToken);
        if (stored is null)
        {
            return UseCaseResult<OrderDto>.Failure(
                "NOT_FOUND", "Không tìm thấy bản tính giá.", "NotFound");
        }
        if (stored.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
        {
            return UseCaseResult<OrderDto>.Failure("QUOTE_EXPIRED", "Bản tính giá hết hạn.", "Conflict");
        }
        var accepted = JsonSerializer.Deserialize<CheckoutQuoteDto>(stored.AcceptedSnapshotJson)!;
        if (accepted.SellerDeliveryFees is null)
        {
            return UseCaseResult<OrderDto>.Failure("MSG118",
                "Bản báo giá cũ chưa lưu phí vận chuyển. Hãy tính lại checkout.", "Conflict");
        }
        var recalculated = await CalculateAsync(accepted.Input, cancellationToken,
            accepted.SellerDeliveryFees);
        if (!recalculated.IsSuccess)
        {
            return UseCaseResult<OrderDto>.Failure(recalculated.Error!.Code,
                recalculated.Error.Message, recalculated.Error.Kind);
        }
        var current = recalculated.Value!;
        if (!SameCommercialTerms(accepted, current))
        {
            return UseCaseResult<OrderDto>.Failure("MSG118",
                "Giá, sản phẩm hoặc chính sách đã thay đổi. Hãy tính lại và xác nhận.", "Conflict");
        }
        var order = BuildOrder(current with { ExpectedDeliveryAt = accepted.ExpectedDeliveryAt }, request);
        repository.AddOrder(order);
        var items = await repository.SelectedItemsAsync(actor.UserId, cancellationToken);
        var batches = await repository.BatchesAsync(
            current.Lines.Select(x => x.ProductId).Distinct().ToList(), cancellationToken);
        foreach (var seller in order.SellerOrders)
        {
            foreach (var line in current.Lines.Where(x => x.FarmerId == seller.FarmerProfileId))
            {
                AddOrderLine(seller, line, batches, order, request.RequestKey);
            }
        }
        AddVoucherReservation(order, current);
        if (current.Input.Points > 0)
        {
            repository.AddRewardEntry(new RewardPointEntry
            {
                UserId = actor.UserId, Order = order, EntryType = "Hold",
                Points = current.Input.Points, RequestKey = $"checkout:{request.RequestKey}",
                OccurredAt = clock.GetUtcNow().UtcDateTime
            });
        }
        repository.RemoveCartItems(items);
        accounts.AddAudit(new AuditLog
        {
            EntityType = "Order", Action = "RetailCheckout", PerformedByUserId = actor.UserId,
            NewValue = JsonSerializer.Serialize(new { order.OrderCode, order.FinalAmount }),
            OccurredAt = clock.GetUtcNow().UtcDateTime, TraceId = actor.TraceId
        });
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UseCaseResult<OrderDto>.Success(ToOrderDto(order));
    }

    public async Task<UseCaseResult<OrderDto>> GetOrderAsync(long orderId,
        CancellationToken cancellationToken)
    {
        if (!await IsRetailCustomerAsync(cancellationToken))
        {
            return Denied<OrderDto>();
        }
        var order = await repository.GetOrderAsync(actor.UserId, orderId, cancellationToken);
        return order is null ? UseCaseResult<OrderDto>.Failure("NOT_FOUND",
            "Không tìm thấy đơn hàng.", "NotFound") : UseCaseResult<OrderDto>.Success(ToOrderDto(order));
    }

    private async Task<UseCaseResult<CheckoutQuoteDto>> CalculateAsync(CheckoutRequest request,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<long, decimal>? fixedSellerDeliveryFees = null)
    {
        var validation = await new CheckoutValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UseCaseResult<CheckoutQuoteDto>.Validation(validation.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).ToArray()));
        }
        var normalizedAddress = addressCatalog.Normalize(request.Address);
        if (normalizedAddress is null)
        {
            return UseCaseResult<CheckoutQuoteDto>.Failure("ADDRESS_INVALID",
                "Tỉnh/thành hoặc phường/xã không hợp lệ. Vui lòng chọn lại địa chỉ.");
        }
        var input = request with
        {
            Address = normalizedAddress
        };
        var items = await repository.SelectedItemsAsync(actor.UserId, cancellationToken);
        if (items.Count == 0)
        {
            return UseCaseResult<CheckoutQuoteDto>.Failure("CART_EMPTY", "Chưa chọn sản phẩm thanh toán.");
        }
        var now = clock.GetUtcNow();
        var days = shippingRates.EstimatedDeliveryDays(input.DeliveryMethod);
        var deliveryAt = now.AddDays(days);
        var deliveryDate = DateOnly.FromDateTime(
            deliveryAt.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var batches = await repository.BatchesAsync(
            items.Select(x => x.ProductId).Distinct().ToList(), cancellationToken);
        var remaining = batches.ToDictionary(x => x.HarvestBatchId, x => x.AvailableQuantity);
        var lines = new List<CheckoutLine>();
        // Allocate explicit batch selections before flexible lines.
        foreach (var item in items.OrderByDescending(x => x.HarvestBatchId.HasValue)
            .ThenBy(x => x.CartItemId))
        {
            var product = item.Product;
            if (item.PreOrderCampaignId.HasValue || item.Quantity <= 0
                || item.Quantity != decimal.Round(item.Quantity, 3)
                || product.BaseRetailPrice <= 0 || product.ApprovalStatus != "Approved"
                || !product.IsPublished || !product.Category.IsActive
                || await repository.IsCategoryArchivedAsync(product.CategoryId, cancellationToken)
                || product.FarmerProfile.User.Status != "Active"
                || product.FarmerProfile.VerificationStatus != "Approved"
                || !product.ProductImages.Any(image => image.IsPrimary))
            {
                return StockFailure(product.ProductName);
            }
            var needed = item.Quantity;
            var allocations = new List<BatchAllocation>();
            foreach (var batch in batches.Where(x => x.ProductId == item.ProductId
                && (!item.HarvestBatchId.HasValue || x.HarvestBatchId == item.HarvestBatchId)
                && x.IsReservableForCheckout(DateOnly.FromDateTime(now.UtcDateTime))
                && (!x.ExpirationDate.HasValue || x.ExpirationDate > deliveryDate))
                .OrderBy(x => x.ExpirationDate ?? DateOnly.MaxValue)
                .ThenBy(x => x.ActualHarvestDate).ThenBy(x => x.HarvestBatchId))
            {
                var quantity = Math.Min(needed, remaining[batch.HarvestBatchId]);
                if (quantity <= 0)
                {
                    continue;
                }
                allocations.Add(new BatchAllocation(
                    batch.HarvestBatchId, batch.BatchCode, quantity));
                remaining[batch.HarvestBatchId] -= quantity;
                needed -= quantity;
                if (needed == 0)
                {
                    break;
                }
            }
            if (needed > 0)
            {
                return StockFailure(product.ProductName);
            }
            var price = RetailPricing.UnitPrice(product,
                items.Where(x => x.ProductId == item.ProductId).Sum(x => x.Quantity));
            if (price <= 0 || price > product.BaseRetailPrice)
            {
                return StockFailure(product.ProductName);
            }
            lines.Add(Line(item, allocations, price));
        }
        var subtotal = lines.Sum(x => x.Subtotal);
        decimal deliveryFee = 0;
        var sellerDeliveryFees = new Dictionary<long, decimal>();
        var sellerGroups = lines.GroupBy(line => line.FarmerId).ToList();
        if (fixedSellerDeliveryFees is not null)
        {
            if (fixedSellerDeliveryFees.Count != sellerGroups.Count
                || sellerGroups.Any(group => !fixedSellerDeliveryFees.TryGetValue(group.Key, out var fee)
                    || fee < 0))
            {
                return UseCaseResult<CheckoutQuoteDto>.Failure("MSG118",
                    "Phí vận chuyển trong báo giá không khớp với giỏ hàng hiện tại. Hãy tính lại checkout.",
                    "Conflict");
            }
            foreach (var group in sellerGroups)
            {
                var fee = fixedSellerDeliveryFees[group.Key];
                sellerDeliveryFees[group.Key] = fee;
                deliveryFee += fee;
            }
        }
        else
        {
            foreach (var sellerLines in sellerGroups)
            {
                var shippingRate = await shippingRates.GetRateAsync(new ShippingRateRequest(
                    sellerLines.Key, input.Address,
                    sellerLines.Select(line => new ShippingPackageLine(line.Unit, line.Quantity)).ToList(),
                    sellerLines.Sum(line => line.Subtotal), input.DeliveryMethod), cancellationToken);
                if (shippingRate is null)
                {
                    return UseCaseResult<CheckoutQuoteDto>.Failure("SHIPPING_RATE_UNAVAILABLE",
                        "Chưa thể tính phí GHTK. Vui lòng kiểm tra cấu hình kết nối, địa chỉ lấy hàng của nông trại và đơn vị tính khối lượng.");
                }
                if (!shippingRate.IsSupported || shippingRate.Fee is null)
                {
                    return UseCaseResult<CheckoutQuoteDto>.Failure("MSG117",
                        "GHTK chưa hỗ trợ giao đến địa chỉ này.");
                }
                deliveryFee += shippingRate.Fee.Value;
                sellerDeliveryFees[sellerLines.Key] = shippingRate.Fee.Value;
            }
        }
        var voucher = string.IsNullOrWhiteSpace(input.VoucherCode) ? null
            : await repository.VoucherAsync(input.VoucherCode.Trim(), cancellationToken);
        var voucherBase = voucher?.FarmerProfileId is long farmerId
            ? lines.Where(x => x.FarmerId == farmerId).Sum(x => x.Subtotal) : subtotal;
        var voucherDiscount = 0m;
        string? warning = null;
        if (!string.IsNullOrWhiteSpace(input.VoucherCode))
        {
            var reserved = voucher is null ? 0
                : await repository.ReservedVoucherCountAsync(voucher.VoucherId, cancellationToken);
            if (voucher is null || !voucher.IsActive || voucher.StartDate > now.UtcDateTime
                || voucher.EndDate <= now.UtcDateTime || voucherBase < voucher.MinOrderAmount
                || voucherBase <= 0 || voucher.DiscountValue <= 0
                || voucher.DiscountType is not ("Percentage" or "FixedAmount")
                || voucher.DiscountType == "Percentage" && voucher.DiscountValue > 100
                || voucher.UsageLimit.HasValue
                    && voucher.UsedCount + reserved >= voucher.UsageLimit)
            {
                warning = "MSG116: Voucher không hợp lệ hoặc không áp dụng; đã bỏ giảm giá.";
                input = input with { VoucherCode = null };
                voucher = null;
            }
            else
            {
                voucherDiscount = RetailPricing.Money(voucher.DiscountType == "Percentage"
                    ? voucherBase * voucher.DiscountValue / 100 : voucher.DiscountValue);
                voucherDiscount = Math.Min(voucherBase,
                    Math.Min(voucherDiscount, voucher.MaxDiscountAmount ?? voucherBase));
                input = input with { VoucherCode = voucher.VoucherCode };
            }
        }
        var pointsDiscount = 0m;
        if (input.Points > 0)
        {
            if (policy.PointValueVnd is not > 0 || policy.MaximumPointDiscountPercent is null)
            {
                return UseCaseResult<CheckoutQuoteDto>.Failure("POINT_POLICY_UNAVAILABLE",
                    "Chưa cấu hình chính sách quy đổi điểm. Có thể tiếp tục với 0 điểm.");
            }
            pointsDiscount = RetailPricing.Money(input.Points * policy.PointValueVnd.Value);
            var limit = RetailPricing.Money((subtotal - voucherDiscount)
                * policy.MaximumPointDiscountPercent.Value / 100);
            if (input.Points > await repository.PointsAsync(actor.UserId, cancellationToken)
                || pointsDiscount > limit)
            {
                return UseCaseResult<CheckoutQuoteDto>.Failure("POINTS_INVALID",
                    "Điểm vượt số dư hoặc mức giảm cho phép.");
            }
        }
        return UseCaseResult<CheckoutQuoteDto>.Success(new CheckoutQuoteDto(Guid.NewGuid(), input,
            lines.OrderBy(x => x.CartItemId).ToList(), subtotal, deliveryFee, voucherDiscount,
            pointsDiscount, subtotal + deliveryFee - voucherDiscount - pointsDiscount,
            now.AddMinutes(policy.QuoteMinutes), deliveryAt, voucher?.VoucherId,
            voucher?.FarmerProfileId, warning, sellerDeliveryFees));
    }

    private Order BuildOrder(CheckoutQuoteDto quote, PlaceOrderRequest request)
    {
        var now = clock.GetUtcNow();
        var address = quote.Input.Address;
        var order = new Order
        {
            BuyerUserId = actor.UserId, OrderCode = $"RC-{Guid.NewGuid():N}", OrderType = "Retail",
            OrderDate = now.UtcDateTime,
            ExpectedDeliveryDate = quote.ExpectedDeliveryAt.UtcDateTime,
            ShippingAddress = $"{address.Detail}, {address.Ward}, {address.Province}",
            RecipientNameSnapshot = address.RecipientName, RecipientPhoneSnapshot = address.Phone,
            ProvinceCodeSnapshot = address.ProvinceCode, ProvinceNameSnapshot = address.Province,
            WardCodeSnapshot = address.WardCode, WardNameSnapshot = address.Ward,
            AddressDetailSnapshot = address.Detail,
            AddressCatalogVersionSnapshot = address.CatalogVersion,
            TotalAmount = quote.Subtotal, DeliveryFee = quote.DeliveryFee,
            DiscountAmount = quote.VoucherDiscount + quote.PointsDiscount,
            FinalAmount = quote.Total,
            VoucherDiscount = quote.VoucherDiscount, PointDiscount = quote.PointsDiscount,
            RedeemedPoints = quote.Input.Points, VoucherCodeSnapshot = quote.Input.VoucherCode,
            OrderStatus = "PendingPayment", ReservationExpiresAt = now.AddMinutes(
                policy.ReservationMinutes).UtcDateTime,
            RetailQuoteId = request.QuoteId, CheckoutRequestKey = request.RequestKey,
            CheckoutCommittedAt = now.UtcDateTime, DeliveryMethod = quote.Input.DeliveryMethod,
            PaymentMethod = quote.Input.PaymentMethod, OrderNotes = quote.Input.Notes
        };
        var groups = quote.Lines.GroupBy(x => x.FarmerId).OrderBy(x => x.Key).ToList();
        var subtotals = groups.Select(x => x.Sum(line => line.Subtotal)).ToList();
        var voucherShares = quote.VoucherFarmerId is null
            ? RetailPricing.Split(quote.VoucherDiscount, subtotals)
            : groups.Select(x => x.Key == quote.VoucherFarmerId ? quote.VoucherDiscount : 0m)
                .ToList();
        var pointWeights = subtotals.Select((x, index) => x - voucherShares[index]).ToList();
        var pointShares = pointWeights.Sum() == 0 ? groups.Select(_ => 0m).ToList()
            : RetailPricing.Split(quote.PointsDiscount, pointWeights);
        for (var index = 0; index < groups.Count; index++)
        {
            var discount = voucherShares[index] + pointShares[index];
            var fee = quote.SellerDeliveryFees?.GetValueOrDefault(groups[index].Key)
                ?? quote.DeliveryFee / groups.Count;
            order.SellerOrders.Add(new SellerOrder
            {
                FarmerProfileId = groups[index].Key,
                SellerNameSnapshot = groups[index].First().SellerName,
                SellerOrderCode = $"{order.OrderCode}-{index + 1}", Subtotal = subtotals[index],
                DiscountShare = discount, DeliveryFee = fee,
                FinalAmount = subtotals[index] + fee - discount, Status = "PendingPayment"
            });
        }
        return order;
    }

    private void AddOrderLine(SellerOrder seller, CheckoutLine line,
        IReadOnlyList<HarvestBatch> batches, Order order, string key)
    {
        var item = new OrderItem
        {
            ProductId = line.ProductId, ProductNameSnapshot = line.ProductName,
            Quantity = line.Quantity, UnitPriceSnapshot = line.UnitPrice, UnitSnapshot = line.Unit,
            SubTotal = line.Subtotal, ItemStatus = "Allocated"
        };
        seller.OrderItems.Add(item);
        var sequence = 0;
        foreach (var allocation in line.Allocations)
        {
            var batch = batches.Single(x => x.HarvestBatchId == allocation.BatchId);
            batch.Reserve(allocation.Quantity,
                DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
            var reservation = new OrderItemBatchAllocation
            {
                HarvestBatchId = batch.HarvestBatchId, AllocatedQuantity = allocation.Quantity,
                AllocationSequence = ++sequence, AllocationStatus = "Reserved",
                ReservedAt = clock.GetUtcNow().UtcDateTime, ExpiresAt = order.ReservationExpiresAt
            };
            item.OrderItemBatchAllocations.Add(reservation);
            repository.AddMovement(new InventoryMovement
            {
                HarvestBatchId = batch.HarvestBatchId, OrderItemBatchAllocation = reservation,
                RecordedByUserId = actor.UserId, MovementType = "Reserve",
                QuantityChange = -allocation.Quantity, Reason = order.OrderCode,
                OccurredAt = clock.GetUtcNow().UtcDateTime,
                RequestKey = $"reserve:{key}:{line.CartItemId}:{batch.HarvestBatchId}"
            });
        }
    }

    private void AddVoucherReservation(Order order, CheckoutQuoteDto quote)
    {
        if (quote.VoucherId is not long voucherId)
        {
            return;
        }
        order.VoucherRedemptions.Add(new VoucherRedemption
        {
            VoucherId = voucherId, DiscountSnapshot = quote.VoucherDiscount, Status = "Reserved",
            ReservedAt = clock.GetUtcNow().UtcDateTime, ExpiresAt = order.ReservationExpiresAt,
            SellerOrder = quote.VoucherFarmerId is long farmerId
                ? order.SellerOrders.Single(x => x.FarmerProfileId == farmerId) : null
        });
    }

    private async Task<bool> IsRetailCustomerAsync(CancellationToken cancellationToken)
    {
        var user = await accounts.FindActorAsync(actor.UserId, cancellationToken);
        return user is not null && user.Role == "RetailCustomer" && user.Status == "Active"
            && !user.IsTemporarilyLocked(clock.GetUtcNow());
    }

    private static CheckoutLine Line(CartItem item, IReadOnlyList<BatchAllocation> allocations,
        decimal price)
    {
        return new CheckoutLine(item.CartItemId, item.ProductId, item.Product.FarmerProfileId,
            item.Product.ProductName, item.Product.FarmerProfile.FarmName, item.Product.Unit,
            item.Quantity, price, RetailPricing.Money(price * item.Quantity),
            item.Product.ProductImages.FirstOrDefault(image => image.IsPrimary)?.ImageUrl,
            allocations);
    }

    private static bool SameCommercialTerms(CheckoutQuoteDto accepted, CheckoutQuoteDto current)
    {
        // A new FEFO allocation is allowed when commercial terms stay unchanged and stock exists.
        return accepted.Subtotal == current.Subtotal && accepted.Total == current.Total
            && accepted.DeliveryFee == current.DeliveryFee
            && accepted.VoucherId == current.VoucherId
            && accepted.VoucherDiscount == current.VoucherDiscount
            && accepted.PointsDiscount == current.PointsDiscount
            && (accepted.SellerDeliveryFees ?? new Dictionary<long, decimal>()).OrderBy(x => x.Key)
                .SequenceEqual((current.SellerDeliveryFees ?? new Dictionary<long, decimal>())
                    .OrderBy(x => x.Key))
            && accepted.Input == current.Input
            && accepted.Lines.Select(x => (x.CartItemId, x.ProductId, x.FarmerId, x.Unit,
                x.Quantity, x.UnitPrice, x.Subtotal)).SequenceEqual(current.Lines.Select(x =>
                    (x.CartItemId, x.ProductId, x.FarmerId, x.Unit,
                        x.Quantity, x.UnitPrice, x.Subtotal)));
    }

    private static OrderDto ToOrderDto(Order order, bool duplicate = false)
    {
        return new OrderDto(order.OrderId, order.OrderCode, order.OrderStatus, order.FinalAmount,
            new DateTimeOffset(DateTime.SpecifyKind(order.ReservationExpiresAt!.Value,
                DateTimeKind.Utc)), order.PaymentMethod!, null, duplicate,
            duplicate ? "MSG119: Yêu cầu đã được xử lý. Không tạo thêm đơn hoặc giữ hàng lần nữa."
                : "Checkout đã hoàn tất. Vui lòng chuyển khoản đúng tổng tiền hiển thị trong thông tin đơn hàng.");
    }

    private static UseCaseResult<T> Denied<T>() => UseCaseResult<T>.Failure("FORBIDDEN",
        "Chỉ khách hàng bán lẻ có tài khoản hoạt động được thanh toán.", "Forbidden");

    private static UseCaseResult<CheckoutQuoteDto> StockFailure(string productName)
    {
        return UseCaseResult<CheckoutQuoteDto>.Failure("MSG88",
            $"{productName} không đủ hàng hợp lệ để giao. Hãy cập nhật giỏ hàng.", "Conflict");
    }
}

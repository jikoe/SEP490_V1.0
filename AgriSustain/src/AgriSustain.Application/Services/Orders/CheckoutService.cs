using System.Security.Cryptography;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Domain.Entities.Catalog;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Domain.ValueObjects;
using FluentValidation;

namespace AgriSustain.Application.Services.Orders;

/// <summary>
/// Receives transactional persistence, actor, clock, checkout policies, trusted address catalog
/// and request validators for UC-RC-02 only.
/// </summary>
public sealed class CheckoutService(
    IThreeUcUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider timeProvider,
    ICheckoutPolicy policy, IAddressCatalog addressCatalog,
    IValidator<CheckoutInput> inputValidator,
    IValidator<PlaceOrderRequest> placeValidator) : ICheckoutService
{
    /// <summary>
    /// Loads only the active RetailCustomer own selected-cart context, saved addresses and mock
    /// benefits. It consumes UC-RC-01 output without adding cart CRUD behavior.
    /// </summary>
    public Task<Result<CommerceContext>> GetContextAsync(CancellationToken cancellationToken) =>
        unitOfWork.ReadAsync(async session =>
        {
            var access = await CurrentUserAccess.RequireAsync(
                session, currentUser, "RetailCustomer",
                timeProvider.GetUtcNow(), cancellationToken);
            if (access.Error != null || access.Value == null)
            {
                return Result<CommerceContext>.Failure(access.Error
                    ?? new Error("FORBIDDEN", "Không đủ quyền.", "Forbidden"));
            }
            var context = await session.Checkout.GetContextAsync(access.Value.Id, cancellationToken);
            var selected = new List<OrderLineSnapshot>();
            // Read-only item previews stay visible before a complete address exists. These are
            // not accepted quotes or reservations; Place still recalculates every eligibility,
            // price and FEFO rule. Cart editing remains owned by UC-RC-01.
            foreach (var item in context.Items.Where(item => item.Selected))
            {
                var product = await session.Checkout.FindProductAsync(item.ProductId,
                    cancellationToken);
                if (product == null)
                {
                    continue;
                }
                decimal price = SelectUnitPrice(product, item.Quantity);
                selected.Add(new OrderLineSnapshot(item.Id, product.Id, product.SellerId,
                    product.SellerName, product.Name, product.Unit, item.Quantity, price,
                    Money(price * item.Quantity), [], product.ImageUrl));
            }
            return Result<CommerceContext>.Success(context with { SelectedItems = selected });
        }, cancellationToken);

    /// <summary>
    /// Validates checkout input, builds a server quote and saves it for the buyer. Preview does
    /// not reserve inventory; invalid voucher feedback removes only its discount.
    /// </summary>
    public async Task<Result<CheckoutQuote>> PreviewAsync(
        CheckoutInput input, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(inputValidator, input, cancellationToken);
        if (error != null)
        {
            return Result<CheckoutQuote>.Failure(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            var access = await CurrentUserAccess.RequireAsync(
                session, currentUser, "RetailCustomer",
                timeProvider.GetUtcNow(), cancellationToken);
            if (access.Error != null || access.Value == null)
            {
                return Result<CheckoutQuote>.Failure(access.Error
                    ?? new Error("FORBIDDEN", "Không đủ quyền.", "Forbidden"));
            }

            var quote = await CalculateAsync(session, access.Value.Id, input, cancellationToken);
            if (quote.Value != null)
            {
                await session.Checkout.SaveQuoteAsync(quote.Value, cancellationToken);
            }

            return quote;
        }, cancellationToken);
    }

    /// <summary>
    /// Revalidates the owned quote, prices, FEFO allocation and benefits, then atomically stages
    /// batch reservations, order snapshots, selected-cart removal and audit. Retry/points storage
    /// remains explicitly mocked pending DB-06/09 decisions.
    /// </summary>
    public async Task<Result<OrderResponse>> PlaceAsync(
        PlaceOrderRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(
            placeValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<OrderResponse>.Failure(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            var access = await CurrentUserAccess.RequireAsync(
                session, currentUser, "RetailCustomer", now, cancellationToken);
            if (access.Error != null || access.Value == null)
            {
                return Result<OrderResponse>.Failure(access.Error
                    ?? new Error("FORBIDDEN", "Không đủ quyền.", "Forbidden"));
            }

            long buyerId = access.Value.Id;
            CheckoutQuote? accepted = await session.Checkout.FindQuoteAsync(
                request.QuoteId, cancellationToken);
            if (accepted == null || accepted.BuyerId != buyerId)
            {
                return Result<OrderResponse>.Failure(new Error(
                    "QUOTE_NOT_FOUND", "Không tìm thấy bản xác nhận của bạn.", "NotFound"));
            }

            RetailOrder? existing = await session.Checkout.FindOrderAsync(
                buyerId, request.RequestKey, cancellationToken);
            if (existing != null)
            {
                if (existing.RequestFingerprint != accepted.Fingerprint)
                {
                    return Result<OrderResponse>.Failure(new Error(
                        "REQUEST_KEY_REUSED", "Mã yêu cầu đã dùng cho nội dung khác.", "Conflict"));
                }

                return Result<OrderResponse>.Success(ToResponse(existing, true));
            }

            if (accepted.ExpiresAt <= now)
            {
                return Result<OrderResponse>.Failure(new Error(
                    "QUOTE_EXPIRED", "Bản tính giá đã hết hạn. Vui lòng tính lại.", "Conflict"));
            }

            var recalculated = await CalculateAsync(
                session, buyerId, accepted.Input, cancellationToken);
            if (recalculated.Error != null || recalculated.Value == null)
            {
                return Result<OrderResponse>.Failure(recalculated.Error
                    ?? new Error("CHECKOUT_INVALID", "Vui lòng kiểm tra đơn hàng."));
            }

            CheckoutQuote current = recalculated.Value;
            if (current.WarningCode != null)
            {
                return Result<OrderResponse>.Failure(new Error(
                    current.WarningCode, current.Warning ?? "Vui lòng tính lại giảm giá.",
                    "Conflict"));
            }

            if (current.Fingerprint != accepted.Fingerprint)
            {
                return Result<OrderResponse>.Failure(new Error(
                    "MSG118", "Giá hoặc giỏ hàng đã thay đổi. Hãy xem và xác nhận lại.",
                    "Conflict"));
            }

            var order = new RetailOrder
            {
                Id = await session.Checkout.NextOrderIdAsync(cancellationToken),
                BuyerId = buyerId,
                Code = $"RTL-{Guid.NewGuid():N}".ToUpperInvariant(),
                QuoteId = accepted.Id,
                RequestKey = request.RequestKey,
                RequestFingerprint = accepted.Fingerprint,
                Address = current.Input.Address,
                DeliveryMethod = current.Input.DeliveryMethod,
                PaymentMethod = current.Input.PaymentMethod,
                Notes = current.Input.Notes,
                CreatedAt = now,
                ReservationExpiresAt = now.Add(policy.ReservationLifetime),
                Lines = Array.AsReadOnly(current.Lines.ToArray()),
                Sellers = BuildSellerOrders(current),
                DeliveryFee = current.DeliveryFee,
                VoucherDiscount = current.VoucherDiscount,
                PointsDiscount = current.PointsDiscount,
                RedeemedPoints = current.Input.Points,
                VoucherId = current.VoucherId
            };
            order.EnsureValid();

            foreach (var group in current.Lines.SelectMany(line => line.Allocations)
                .GroupBy(allocation => allocation.BatchId).OrderBy(group => group.Key))
            {
                HarvestBatch? batch = await session.Checkout.FindBatchAsync(
                    group.Key, cancellationToken);
                if (batch == null
                    || !batch.CanReserve(group.Sum(line => line.Quantity), Today(now)))
                {
                    return Result<OrderResponse>.Failure(StockError());
                }

                batch.Reserve(group.Sum(line => line.Quantity), Today(now));
                await session.Checkout.ReserveBatchAsync(batch, cancellationToken);
            }

            await session.Checkout.SaveOrderAsync(order, cancellationToken);
            await session.Checkout.ReserveBenefitsAsync(order, cancellationToken);
            await session.Checkout.RemoveOrderedItemsAsync(buyerId,
                current.Lines.Select(line => line.CartItemId).ToArray(), cancellationToken);
            await session.AddAuditAsync(new AuditRecord(
                buyerId, "Order", order.Id, "place", "null",
                JsonSerializer.Serialize(new
                {
                    order.Code,
                    order.Status,
                    order.Total
                }), null, now),
                cancellationToken);
            return Result<OrderResponse>.Success(ToResponse(order, false));
        }, cancellationToken);
    }

    /// <summary>
    /// Normalizes province/ward names from trusted codes, validates offers and allocates
    /// available batches FEFO before calculating server-side money. Fingerprints include batch
    /// allocations so changed stock cannot silently replace the accepted source lot.
    /// </summary>
    private async Task<Result<CheckoutQuote>> CalculateAsync(
        IThreeUcSession session, long buyerId, CheckoutInput input,
        CancellationToken cancellationToken)
    {
        CommerceContext context = await session.Checkout.GetContextAsync(
            buyerId, cancellationToken);
        CartLine[] selected = context.Items.Where(item => item.Selected)
            .OrderBy(item => item.Id).ToArray();
        if (selected.Length == 0 || selected.Length > 100)
        {
            return Result<CheckoutQuote>.Failure(new Error(
                "MSG87", "Cần chọn từ 1 đến 100 dòng sản phẩm trong giỏ.", "Conflict"));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        AddressSnapshot? normalizedAddress = addressCatalog.Normalize(input.Address);
        if (normalizedAddress == null)
        {
            return Result<CheckoutQuote>.Failure(new Error(
                "MSG117", "Địa chỉ tỉnh/xã không hợp lệ hoặc không cùng địa giới."));
        }

        input = input with
        {
            Address = normalizedAddress
        };
        var lines = new List<OrderLineSnapshot>();
        var remaining = new Dictionary<long, decimal>();
        foreach (CartLine item in selected)
        {
            ProductOffer? product = await session.Checkout.FindProductAsync(
                item.ProductId, cancellationToken);
            if (product == null || !product.IsApproved || !product.IsPublished
                || !product.IsSellerEligible || product.RetailPrice <= 0 || item.Quantity <= 0
                || item.Quantity != decimal.Round(item.Quantity, 3)
                || !await IsCategoryAvailableAsync(session, product.CategoryId, cancellationToken))
            {
                return Result<CheckoutQuote>.Failure(StockError());
            }

            var seller = await session.Accounts.FindAsync(product.SellerUserId, cancellationToken);
            if (seller?.Status != "Active")
            {
                return Result<CheckoutQuote>.Failure(StockError());
            }

            var batches = await session.Checkout.FindBatchesAsync(product.Id, cancellationToken);
            var allocations = AllocateBatches(batches.Where(batch => batch.ProductId == product.Id),
                item.Quantity, Today(now), remaining);
            if (allocations == null)
            {
                return Result<CheckoutQuote>.Failure(StockError());
            }

            decimal unitPrice = SelectUnitPrice(product, item.Quantity);

            lines.Add(new OrderLineSnapshot(item.Id, product.Id, product.SellerId,
                product.SellerName, product.Name, product.Unit, item.Quantity, unitPrice,
                Money(unitPrice * item.Quantity), allocations, product.ImageUrl));
        }

        decimal subtotal = lines.Sum(line => line.Subtotal);
        decimal? fee = policy.GetDeliveryFee(
            input.Address, input.DeliveryMethod,
            lines.Select(line => line.SellerId).Distinct().Count());
        if (fee == null || fee < 0)
        {
            return Result<CheckoutQuote>.Failure(new Error(
                "MSG117", "Địa chỉ này chưa thuộc vùng giao hàng. Vui lòng chọn địa chỉ khác."));
        }

        VoucherOffer? voucher = string.IsNullOrWhiteSpace(input.VoucherCode) ? null
            : await session.Checkout.FindVoucherAsync(input.VoucherCode.Trim(), cancellationToken);
        decimal eligibleSubtotal = voucher?.SellerId is { } sellerId
            ? lines.Where(line => line.SellerId == sellerId).Sum(line => line.Subtotal) : subtotal;
        bool validVoucher = voucher != null && voucher.IsActive
            && voucher.StartsAt <= now && voucher.EndsAt > now && eligibleSubtotal > 0
            && eligibleSubtotal >= voucher.MinOrder && voucher.Value > 0
            && voucher.DiscountType is "Percent" or "Fixed"
            && (voucher.DiscountType != "Percent" || voucher.Value <= 100)
            && (voucher.UsageLimit == null
                || voucher.UsedCount + voucher.ReservedCount < voucher.UsageLimit);
        decimal voucherDiscount = validVoucher && voucher != null ? Money(Math.Min(
            eligibleSubtotal, Math.Min(voucher.MaxDiscount ?? eligibleSubtotal,
                voucher.DiscountType == "Percent"
                    ? eligibleSubtotal * voucher.Value / 100 : voucher.Value))) : 0;
        bool invalidVoucher = !string.IsNullOrWhiteSpace(input.VoucherCode) && !validVoucher;
        decimal? pointsDiscount = policy.GetPointsDiscount(
            input.Points, subtotal - voucherDiscount);
        if (input.Points > context.AvailablePoints || pointsDiscount == null || pointsDiscount < 0
            || pointsDiscount > subtotal - voucherDiscount)
        {
            return Result<CheckoutQuote>.Failure(new Error(
                "INVALID_POINTS", "Điểm yêu cầu vượt số dư hoặc giới hạn áp dụng."));
        }

        var normalized = input with
        {
            VoucherCode = validVoucher ? voucher?.Code : null,
            Notes = input.Notes?.Trim()
        };
        decimal total = subtotal + fee.Value - voucherDiscount - pointsDiscount.Value;
        if (total <= 0)
        {
            return Result<CheckoutQuote>.Failure(new Error(
                "INVALID_TOTAL", "Tổng tiền phải dương."));
        }

        byte[] summaryBytes = JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                buyerId,
                normalized,
                // Product photos are display metadata, not a change to the accepted price,
                // quantity or stock allocation. Keep them out of the checkout fingerprint.
                lines = lines.Select(line => new
                {
                    line.CartItemId, line.ProductId, line.SellerId, line.SellerName,
                    line.ProductName, line.Unit, line.Quantity, line.UnitPrice,
                    line.Subtotal, line.Allocations
                }),
                fee,
                voucherDiscount,
                pointsDiscount,
                total
            });
        string fingerprint = Convert.ToHexString(SHA256.HashData(summaryBytes));
        return Result<CheckoutQuote>.Success(new CheckoutQuote(
            Guid.NewGuid(), buyerId, normalized, fingerprint, lines.AsReadOnly(), subtotal,
            fee.Value, voucherDiscount, pointsDiscount.Value, total,
            validVoucher ? voucher?.Id : null,
            invalidVoucher ? "MSG116" : null,
            invalidVoucher ? "Voucher không hợp lệ. Đã bỏ giảm giá; bạn có thể tiếp tục." : null,
            now.Add(policy.QuoteLifetime), validVoucher ? voucher?.SellerId : null));
    }

    /// <summary>
    /// Allocates the requested quantity across eligible lots by earliest expiry and stable ID.
    /// Shared remaining quantities prevent two cart lines allocating the same stock twice.
    /// Returns null when total eligible stock is insufficient; undated-lot ordering is a
    /// documented demo assumption.
    /// </summary>
    private static IReadOnlyList<BatchAllocationSnapshot>? AllocateBatches(
        IEnumerable<HarvestBatch> batches, decimal quantity, DateOnly today,
        Dictionary<long, decimal> remaining)
    {
        var allocations = new List<BatchAllocationSnapshot>();
        // FEFO is deterministic. Undated batches are last; this fallback is a demo policy
        // for the catalog team to confirm, not a new database requirement.
        foreach (HarvestBatch batch in batches.OrderBy(batch => batch.ExpirationDate == null)
            .ThenBy(batch => batch.ExpirationDate).ThenBy(batch => batch.Id))
        {
            if (batch.AvailableQuantity <= 0
                || !batch.CanReserve(Math.Min(quantity, batch.AvailableQuantity), today))
            {
                continue;
            }

            decimal available = remaining.GetValueOrDefault(batch.Id, batch.AvailableQuantity);
            decimal take = Math.Min(quantity, available);
            if (take <= 0)
            {
                continue;
            }

            allocations.Add(new BatchAllocationSnapshot(batch.Id, batch.Code, take));
            remaining[batch.Id] = available - take;
            quantity -= take;
            if (quantity == 0)
            {
                return allocations.AsReadOnly();
            }
        }

        return null;
    }

    /// <summary>
    /// Groups quoted product lines by seller, allocates only eligible voucher/point shares and
    /// distributes delivery fees. Seller payable amounts must sum exactly to the parent order
    /// total.
    /// </summary>
    private static IReadOnlyList<SellerOrderSnapshot> BuildSellerOrders(CheckoutQuote quote)
    {
        var groups = quote.Lines.GroupBy(line => line.SellerId).OrderBy(g => g.Key).ToArray();
        decimal[] subtotals = groups.Select(g => g.Sum(line => line.Subtotal)).ToArray();
        decimal[] discounts = Split(quote.VoucherDiscount, groups.Select((group, index) =>
            quote.VoucherSellerId == null || quote.VoucherSellerId == group.Key
                ? subtotals[index] : 0m).ToArray());
        decimal[] pointShares = Split(quote.PointsDiscount,
            subtotals.Select((amount, index) => amount - discounts[index]).ToArray());
        decimal[] delivery = Split(quote.DeliveryFee, groups.Select(_ => 1m).ToArray());
        return groups.Select((group, index) => new SellerOrderSnapshot(
            group.Key, $"SELL-{Guid.NewGuid():N}".ToUpperInvariant(), subtotals[index],
            discounts[index] + pointShares[index], delivery[index],
            subtotals[index] + delivery[index] - discounts[index] - pointShares[index])).ToArray();
    }

    /// <summary>
    /// Allocates a money amount by nonnegative weights, truncates intermediate cent shares and
    /// assigns the remainder to the last eligible recipient. Zero amount returns zero shares
    /// without division.
    /// </summary>
    private static decimal[] Split(decimal amount, decimal[] weights)
    {
        var result = new decimal[weights.Length];
        if (amount == 0)
        {
            return result;
        }

        decimal total = weights.Sum();
        decimal allocated = 0;
        int lastEligible = Array.FindLastIndex(weights, weight => weight > 0);
        for (int index = 0; index < weights.Length; index++)
        {
            result[index] = index == lastEligible ? amount - allocated
                : decimal.Floor(amount * weights[index] / total * 100) / 100;
            allocated += result[index];
        }

        return result;
    }

    /// <summary>
    /// Walks the category ancestors to reject missing or hidden classifications. The visited set
    /// is defensive read protection against an infinite loop; it does not reintroduce the
    /// declined category-write constraint.
    /// </summary>
    private static async Task<bool> IsCategoryAvailableAsync(
        IThreeUcSession session, long id, CancellationToken cancellationToken)
    {
        var seen = new HashSet<long>();
        long? categoryId = id;
        while (categoryId != null)
        {
            if (!seen.Add(categoryId.Value))
            {
                return false;
            }

            Category? category = await session.Categories.FindAsync(
                categoryId.Value, cancellationToken);
            if (category == null || !category.IsActive)
            {
                return false;
            }

            categoryId = category.ParentId;
        }

        return true;
    }

    /// <summary>
    /// Returns the immutable pending-payment summary and handoff destination. A null payment
    /// destination means no QR/payment UC exists; no external payment is initiated here.
    /// </summary>
    private OrderResponse ToResponse(RetailOrder order, bool duplicate) => new(
        order.Id, order.Code, order.Status, order.ReservationExpiresAt, order.Subtotal,
        order.DeliveryFee, order.VoucherDiscount, order.PointsDiscount, order.Total,
        order.Sellers, duplicate, policy.GetPaymentDestination(order.Id),
        duplicate ? "MSG119: Đã có đơn cho yêu cầu này; không giữ hàng lần nữa."
            : "Đã tạo đơn chờ thanh toán. Module QR/thanh toán chưa được tích hợp.");

    /// <summary>
    /// Uses the same tier eligibility for the initial cart display and every server quote.
    /// Invalid thresholds or nonpositive/higher tier prices fall back to the retail price.
    /// </summary>
    private static decimal SelectUnitPrice(ProductOffer product, decimal quantity) =>
        product.WholesaleMinQuantity is > 1 && product.WholesalePrice is > 0
        && product.WholesalePrice <= product.RetailPrice
        && quantity >= product.WholesaleMinQuantity
            ? product.WholesalePrice.Value : product.RetailPrice;

    // DEMO-MONEY: the team must confirm rounding/currency policy before SQL integration.
    /// <summary>
    /// Applies the documented demo money rounding to two decimal places. Currency and production
    /// rounding policy still belong to the team contract.
    /// </summary>
    private static decimal Money(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Derives the Vietnam business date from the UTC server instant for DATE-only batch expiry
    /// checks; does not convert historical database timestamps.
    /// </summary>
    private static DateOnly Today(DateTimeOffset now) =>
        DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);

    /// <summary>
    /// Builds MSG88 for a no-longer-sellable offer or insufficient eligible inventory, allowing
    /// rollback and a fresh quote.
    /// </summary>
    private static Error StockError() => new(
        "MSG88", "Có sản phẩm/lô hàng không còn bán được hoặc không đủ tồn kho.", "Conflict");
}

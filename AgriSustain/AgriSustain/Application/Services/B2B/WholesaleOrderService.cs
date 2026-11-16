using System.Globalization;
using System.Text;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.B2B;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.B2B;

public class WholesaleOrderService(
    IWholesaleOrderRepository repository,
    IShippingRateProvider? shippingRateProvider = null,
    IAddressCatalog? addressCatalog = null) : IWholesaleOrderService
{
    private readonly IWholesaleOrderRepository _repository = repository;
    private readonly IShippingRateProvider? _shippingRateProvider = shippingRateProvider;
    private readonly IAddressCatalog? _addressCatalog = addressCatalog;

    public async Task<Result<WholesaleProductInfoDto>> GetWholesaleProductInfoAsync(
        long productId,
        long buyerUserId,
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetWholesaleProductAsync(productId, cancellationToken);
        if (product == null)
        {
            return Result<WholesaleProductInfoDto>.Fail("Không tìm thấy thông tin nông sản.", 404);
        }

        if (!product.IsPublished || !string.Equals(product.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            return Result<WholesaleProductInfoDto>.Fail("Nông sản này hiện chưa mở bán công khai hoặc chưa được kiểm duyệt.", 400);
        }

        if (!product.WholesalePrice.HasValue || !product.WholesaleMinQuantity.HasValue ||
            product.WholesalePrice.Value <= 0 || product.WholesaleMinQuantity.Value <= 0)
        {
            return Result<WholesaleProductInfoDto>.Fail("Sản phẩm này chưa cấu hình chính sách đặt hàng sỉ / bán buôn B2B.", 400);
        }

        var buyerProfile = await _repository.GetBuyerKycProfileAsync(buyerUserId, cancellationToken);
        var buyerUser = await _repository.GetUserByIdAsync(buyerUserId, cancellationToken);

        var isB2BUser = buyerUser == null || string.Equals(buyerUser.Role, "WholesaleBuyer", StringComparison.OrdinalIgnoreCase);
        var isApprovedKyc = isB2BUser
            ? (buyerProfile != null && string.Equals(buyerProfile.VerificationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            : (buyerUser != null && string.Equals(buyerUser.Status, "Active", StringComparison.OrdinalIgnoreCase));

        var personalAddr = buyerUser?.DeliveryAddress;
        var personalAddressText = personalAddr != null
            ? string.Join(", ", new[] { personalAddr.AddressDetail, personalAddr.Ward, personalAddr.District, personalAddr.ProvinceCity }
                .Where(s => !string.IsNullOrWhiteSpace(s)))
            : string.Empty;

        var buyerKycDto = new WholesaleBuyerCorporateKycDto(
            UserId: buyerUserId,
            CompanyName: buyerProfile?.CompanyName ?? (!isB2BUser ? (buyerUser?.FullName ?? string.Empty) : string.Empty),
            TaxCode: buyerProfile?.TaxCode ?? string.Empty,
            RepresentativeName: buyerProfile?.RepresentativeName
                ?? (!string.IsNullOrWhiteSpace(personalAddr?.RecipientName) ? personalAddr.RecipientName : (buyerUser?.FullName ?? string.Empty)),
            BusinessAddress: !string.IsNullOrWhiteSpace(buyerProfile?.BusinessAddress)
                ? buyerProfile.BusinessAddress
                : personalAddressText,
            PhoneNumber: !string.IsNullOrWhiteSpace(personalAddr?.RecipientPhone)
                ? personalAddr.RecipientPhone
                : buyerUser?.PhoneNumber,
            VerificationStatus: buyerProfile?.VerificationStatus ?? (isApprovedKyc ? "Approved" : "NotSubmitted"),
            IsApprovedKyc: isApprovedKyc);

        var batches = await _repository.GetAvailableHarvestBatchesAsync(productId, cancellationToken);
        var totalStock = batches.Sum(b => b.AvailableQuantity);

        var moq = product.WholesaleMinQuantity.Value;
        var wholesaleBasePrice = product.WholesalePrice.Value;

        var priceTiers = BuildPriceTiers(moq, wholesaleBasePrice);

        var primaryImage = product.ProductImages.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                           ?? product.ProductImages.FirstOrDefault()?.ImageUrl;

        var certs = (product.FarmerProfile?.FarmCertificates ?? [])
            .Where(c => string.Equals(c.VerificationStatus, "Verified", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.VerificationStatus, "Approved", StringComparison.OrdinalIgnoreCase))
            .Select(c => c.CertificateType)
            .Distinct()
            .ToList();

        var availableBatchDtos = batches
            .Select(b => new WholesaleAvailableBatchDto(
                HarvestBatchId: b.HarvestBatchId,
                BatchCode: b.BatchCode,
                AvailableQuantity: b.AvailableQuantity,
                ActualHarvestDate: b.ActualHarvestDate?.ToString("dd/MM/yyyy"),
                Status: b.Status))
            .ToList();

        var activeVouchers = await _repository.GetActiveVouchersAsync(product.FarmerProfileId, cancellationToken);
        var customerUses = await _repository.GetCustomerVoucherUsesAsync(buyerUserId, cancellationToken);
        var initialSubtotal = Math.Round(moq * wholesaleBasePrice, 0);
        var farmName = product.FarmerProfile?.FarmName ?? string.Empty;

        var availableVoucherDtos = activeVouchers
            .Select(v =>
            {
                var usedByCustomer = customerUses.GetValueOrDefault(v.VoucherId);
                var eligible = initialSubtotal > 0 &&
                               initialSubtotal >= v.MinOrderAmount &&
                               usedByCustomer < v.PerCustomerLimit;
                return new WholesaleVoucherOptionDto(
                    Code: v.VoucherCode,
                    DiscountType: v.DiscountType,
                    DiscountValue: v.DiscountValue,
                    MaxDiscountAmount: v.MaxDiscountAmount,
                    MinOrderAmount: v.MinOrderAmount,
                    EndDate: v.EndDate,
                    FarmerProfileId: v.FarmerProfileId,
                    FarmName: v.FarmerProfileId.HasValue ? farmName : null,
                    Eligible: eligible);
            })
            .ToList();

        var dto = new WholesaleProductInfoDto(
            ProductId: product.ProductId,
            ProductName: product.ProductName,
            CategoryName: product.Category?.CategoryName ?? "Nông sản tiêu chuẩn",
            Unit: product.Unit,
            BaseRetailPrice: product.BaseRetailPrice,
            WholesalePrice: wholesaleBasePrice,
            WholesaleMinQuantity: moq,
            AvailableStock: totalStock,
            PrimaryImageUrl: primaryImage,
            Certifications: certs,
            FarmerProfileId: product.FarmerProfileId,
            FarmName: farmName,
            RepresentativeName: product.FarmerProfile?.RepresentativeName ?? string.Empty,
            FarmAddress: product.FarmerProfile?.Address ?? string.Empty,
            FarmAvatarUrl: product.FarmerProfile?.User?.AvatarUrl,
            PriceTiers: priceTiers,
            BuyerKyc: buyerKycDto,
            AvailableBatches: availableBatchDtos,
            AvailableVouchers: availableVoucherDtos);

        return Result<WholesaleProductInfoDto>.Ok(dto);
    }

    public async Task<Result<WholesalePriceCalculationResponse>> CalculatePricingAsync(
        WholesalePriceCalculationRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _repository.GetWholesaleProductAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result<WholesalePriceCalculationResponse>.Fail("Không tìm thấy thông tin nông sản.", 404);
        }

        if (!product.WholesalePrice.HasValue || !product.WholesaleMinQuantity.HasValue)
        {
            return Result<WholesalePriceCalculationResponse>.Fail("Sản phẩm chưa thiết lập chính sách bán buôn.", 400);
        }

        var batches = await _repository.GetAvailableHarvestBatchesAsync(request.ProductId, cancellationToken);
        var availableStock = batches.Sum(b => b.AvailableQuantity);

        var moq = product.WholesaleMinQuantity.Value;
        var wholesaleBasePrice = product.WholesalePrice.Value;

        bool isValid = true;
        string? errorMessage = null;

        if (request.Quantity < moq)
        {
            isValid = false;
            errorMessage = $"Số lượng đặt mua phải từ {moq:N0} {product.Unit} trở lên (Mức tối thiểu MOQ).";
        }
        else if (request.Quantity > availableStock)
        {
            isValid = false;
            errorMessage = $"Số lượng đặt mua vượt quá số lượng tồn kho khả dụng ({availableStock:N0} {product.Unit}).";
        }

        var (tierName, discountPercent, unitPrice) = DetermineTier(request.Quantity, moq, wholesaleBasePrice);

        var subtotal = Math.Round(request.Quantity * unitPrice, 0);
        var retailTotal = Math.Round(request.Quantity * product.BaseRetailPrice, 0);
        var totalSavings = Math.Max(0, retailTotal - subtotal);

        const decimal vatRate = 0m;
        var vatAmount = Math.Round(subtotal * vatRate, 0);
        var logisticsFee = await ResolveLogisticsFeeAsync(
            product: product,
            quantity: request.Quantity,
            subtotal: subtotal,
            buyerUserId: request.BuyerUserId,
            provinceCode: request.ProvinceCode,
            districtCode: request.DistrictCode,
            deliveryAddress: request.DeliveryAddress,
            deliveryMethod: request.DeliveryMethod,
            cancellationToken: cancellationToken);

        var (_, voucherDiscount, appliedVoucherCode, voucherWarning) = await EvaluateVoucherAsync(
            voucherCode: request.VoucherCode,
            subtotal: subtotal,
            farmerProfileId: product.FarmerProfileId,
            buyerUserId: request.BuyerUserId ?? 0,
            cancellationToken: cancellationToken);

        var finalTotal = Math.Max(0m, subtotal + vatAmount + logisticsFee - voucherDiscount);

        var response = new WholesalePriceCalculationResponse(
            Quantity: request.Quantity,
            BaseRetailPrice: product.BaseRetailPrice,
            WholesaleBasePrice: wholesaleBasePrice,
            AppliedUnitPrice: unitPrice,
            ActiveTierName: tierName,
            TierDiscountPercent: discountPercent,
            LineSubtotal: subtotal,
            TotalSavings: totalSavings + voucherDiscount,
            VatRate: vatRate,
            VatAmount: vatAmount,
            LogisticsFee: logisticsFee,
            FinalTotal: finalTotal,
            IsValidQuantity: isValid,
            ErrorMessage: errorMessage,
            VoucherDiscount: voucherDiscount,
            AppliedVoucherCode: appliedVoucherCode,
            VoucherWarning: voucherWarning);

        return Result<WholesalePriceCalculationResponse>.Ok(response);
    }

    public async Task<Result<WholesaleOrderConfirmationDto>> CreateWholesaleOrderAsync(
        CreateWholesaleOrderRequest request,
        long buyerUserId,
        CancellationToken cancellationToken)
    {
        // Precondition 1 & 2: Check buyer account and approved business KYC
        var buyerUser = await _repository.GetUserByIdAsync(buyerUserId, cancellationToken);
        if (buyerUser == null || !string.Equals(buyerUser.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return Result<WholesaleOrderConfirmationDto>.Fail("Tài khoản người mua không hợp lệ hoặc đã bị khóa.", 403);
        }

        var isB2BUser = string.Equals(buyerUser.Role, "WholesaleBuyer", StringComparison.OrdinalIgnoreCase);
        var buyerKyc = await _repository.GetBuyerKycProfileAsync(buyerUserId, cancellationToken);
        if (isB2BUser && (buyerKyc == null || !string.Equals(buyerKyc.VerificationStatus, "Approved", StringComparison.OrdinalIgnoreCase)))
        {
            return Result<WholesaleOrderConfirmationDto>.Fail(
                "Tài khoản doanh nghiệp chưa hoàn tất duyệt hồ sơ KYC (VerificationStatus phải là 'Approved' để đặt hàng sỉ B2B). Vui lòng gửi và chờ phê duyệt hồ sơ tại trang Hồ Sơ Doanh Nghiệp.",
                403);
        }

        var warehouseAddress = !string.IsNullOrWhiteSpace(request.WarehouseAddress)
            ? request.WarehouseAddress
            : request.DeliveryAddress;
        var paymentMethod = !string.IsNullOrWhiteSpace(request.PaymentMethod)
            ? request.PaymentMethod
            : (!string.IsNullOrWhiteSpace(request.PaymentTerms) ? request.PaymentTerms : "BankTransfer");
        var orderNotes = !string.IsNullOrWhiteSpace(request.OrderNotes)
            ? request.OrderNotes
            : request.Notes;

        var companyName = !string.IsNullOrWhiteSpace(request.CompanyName)
            ? request.CompanyName.Trim()
            : (!isB2BUser ? buyerUser.FullName : string.Empty);
        var taxCode = !string.IsNullOrWhiteSpace(request.TaxCode)
            ? request.TaxCode.Trim()
            : (!isB2BUser ? "Cá nhân" : string.Empty);

        // AF4: Validation for delivery and corporate invoice details
        if (string.IsNullOrWhiteSpace(warehouseAddress) ||
            string.IsNullOrWhiteSpace(request.RecipientName) ||
            string.IsNullOrWhiteSpace(request.RecipientPhone) ||
            string.IsNullOrWhiteSpace(companyName) ||
            string.IsNullOrWhiteSpace(taxCode))
        {
            return Result<WholesaleOrderConfirmationDto>.Fail(
                "Vui lòng cung cấp đầy đủ thông tin giao hàng (địa chỉ kho, người nhận, số điện thoại) và thông tin xuất hóa đơn doanh nghiệp (Tên công ty, Mã số thuế).",
                400);
        }

        // Precondition 3: Wholesale-eligible product
        var product = await _repository.GetWholesaleProductAsync(request.ProductId, cancellationToken);
        if (product == null)
        {
            return Result<WholesaleOrderConfirmationDto>.Fail("Không tìm thấy thông tin sản phẩm.", 404);
        }

        if (!product.IsPublished || !string.Equals(product.ApprovalStatus, "Approved", StringComparison.OrdinalIgnoreCase))
        {
            return Result<WholesaleOrderConfirmationDto>.Fail("Sản phẩm hiện không mở bán trên sàn giao dịch.", 400);
        }

        if (!product.WholesalePrice.HasValue || !product.WholesaleMinQuantity.HasValue)
        {
            return Result<WholesaleOrderConfirmationDto>.Fail("Sản phẩm chưa kích hoạt chính sách bán buôn B2B.", 400);
        }

        var moq = product.WholesaleMinQuantity.Value;
        var wholesaleBasePrice = product.WholesalePrice.Value;

        // Query available batches for allocation
        var batches = await _repository.GetAvailableHarvestBatchesAsync(request.ProductId, cancellationToken);
        var totalAvailable = batches.Sum(b => b.AvailableQuantity);

        // AF3: Stock out
        if (totalAvailable <= 0)
        {
            return Result<WholesaleOrderConfirmationDto>.Fail("Nông sản đã hết hàng trong kho. Vui lòng chọn mùa vụ khác hoặc quay lại sau.", 400);
        }

        // AF1: Invalid quantity check
        if (request.Quantity < moq || request.Quantity > totalAvailable)
        {
            return Result<WholesaleOrderConfirmationDto>.Fail(
                $"Số lượng đặt mua phải từ {moq:N0} {product.Unit} trở lên và không vượt quá số lượng tồn kho khả dụng ({totalAvailable:N0} {product.Unit}).",
                400);
        }

        // Pricing calculations (Step 8 & 9)
        var (_, _, unitPrice) = DetermineTier(request.Quantity, moq, wholesaleBasePrice);
        var subtotal = Math.Round(request.Quantity * unitPrice, 0);
        var logisticsFee = await ResolveLogisticsFeeAsync(
            product: product,
            quantity: request.Quantity,
            subtotal: subtotal,
            buyerUserId: buyerUserId,
            provinceCode: request.ProvinceCode,
            districtCode: request.DistrictCode,
            deliveryAddress: warehouseAddress,
            deliveryMethod: "Standard",
            cancellationToken: cancellationToken);
        const decimal vatRate = 0m;
        var vatAmount = Math.Round(subtotal * vatRate, 0);

        var (appliedVoucher, voucherDiscount, appliedVoucherCode, _) = await EvaluateVoucherAsync(
            voucherCode: request.VoucherCode,
            subtotal: subtotal,
            farmerProfileId: product.FarmerProfileId,
            buyerUserId: buyerUserId,
            cancellationToken: cancellationToken);

        var finalAmount = Math.Max(0m, subtotal + vatAmount + logisticsFee - voucherDiscount);

        // Build Order
        var nowUtc = DateTime.UtcNow;
        var reservationExpiresAt = nowUtc.AddMinutes(15);
        var orderCode = $"WS-{nowUtc:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
        var expectedDelivery = request.ExpectedDeliveryDate ?? nowUtc.AddDays(3);

        var order = new Order
        {
            BuyerUserId = buyerUserId,
            OrderCode = orderCode,
            OrderType = "Wholesale",
            OrderStatus = "Pending", // Pending Confirmation / Pending Payment
            OrderDate = nowUtc,
            ReservationExpiresAt = reservationExpiresAt,
            ExpectedDeliveryDate = expectedDelivery,
            ShippingAddress = warehouseAddress.Trim(),
            RecipientNameSnapshot = request.RecipientName.Trim(),
            RecipientPhoneSnapshot = request.RecipientPhone.Trim(),
            ProvinceCodeSnapshot = request.ProvinceCode,
            ProvinceNameSnapshot = request.ProvinceName,
            DistrictCodeSnapshot = request.DistrictCode,
            DistrictNameSnapshot = request.DistrictName,
            WardCodeSnapshot = request.WardCode,
            WardNameSnapshot = request.WardName,
            AddressDetailSnapshot = warehouseAddress.Trim(),
            TotalAmount = subtotal,
            DiscountAmount = voucherDiscount,
            VoucherDiscount = voucherDiscount,
            VoucherCodeSnapshot = appliedVoucherCode,
            DeliveryFee = logisticsFee,
            FinalAmount = finalAmount,
            PaymentMethod = paymentMethod,
            OrderNotes = $"[Hóa đơn DN: {companyName} - MST: {taxCode}] " + (orderNotes?.Trim() ?? string.Empty),
            CheckoutRequestKey = !string.IsNullOrWhiteSpace(request.RequestKey) ? request.RequestKey : Guid.NewGuid().ToString("N")
        };

        var sellerOrder = new SellerOrder
        {
            FarmerProfileId = product.FarmerProfileId,
            SellerNameSnapshot = product.FarmerProfile?.FarmName ?? "Nông Trại Cung Ứng",
            SellerOrderCode = $"{orderCode}-1",
            Subtotal = subtotal,
            DiscountShare = voucherDiscount,
            DeliveryFee = logisticsFee,
            FinalAmount = finalAmount,
            Status = "Pending" // Pending Confirmation
        };

        if (appliedVoucher != null && voucherDiscount > 0)
        {
            order.VoucherRedemptions.Add(new VoucherRedemption
            {
                VoucherId = appliedVoucher.VoucherId,
                DiscountSnapshot = voucherDiscount,
                Status = "Reserved",
                ReservedAt = nowUtc,
                ExpiresAt = reservationExpiresAt,
                SellerOrder = appliedVoucher.FarmerProfileId.HasValue ? sellerOrder : null
            });
        }

        var orderItem = new OrderItem
        {
            ProductId = product.ProductId,
            ProductNameSnapshot = product.ProductName,
            Quantity = request.Quantity,
            UnitPriceSnapshot = unitPrice,
            UnitSnapshot = product.Unit,
            SubTotal = subtotal,
            ItemStatus = "Allocated"
        };

        // Inventory Batch Reservation (Postcondition 3)
        decimal remainingQuantity = request.Quantity;
        var batchAllocations = new List<WholesaleBatchAllocationDto>();
        int sequence = 0;

        foreach (var batch in batches)
        {
            if (remainingQuantity <= 0) break;
            decimal allocate = Math.Min(batch.AvailableQuantity, remainingQuantity);
            if (allocate <= 0) continue;

            batch.Reserve(allocate, DateOnly.FromDateTime(DateTime.UtcNow));

            var allocation = new OrderItemBatchAllocation
            {
                HarvestBatchId = batch.HarvestBatchId,
                AllocatedQuantity = allocate,
                AllocationSequence = ++sequence,
                AllocationStatus = "Reserved",
                ReservedAt = nowUtc,
                ExpiresAt = reservationExpiresAt
            };

            orderItem.OrderItemBatchAllocations.Add(allocation);
            batchAllocations.Add(new WholesaleBatchAllocationDto(batch.HarvestBatchId, batch.BatchCode, allocate, product.Unit));

            remainingQuantity -= allocate;
        }

        sellerOrder.OrderItems.Add(orderItem);
        order.SellerOrders.Add(sellerOrder);

        // Step 12: Persist Order and Reservations
        await _repository.CreateOrderAsync(order, cancellationToken);

        // Notifications (Postcondition 6 & 7)
        if (product.FarmerProfile != null)
        {
            await _repository.AddNotificationAsync(new Notification
            {
                UserId = product.FarmerProfile.UserId,
                Title = "Đơn hàng sỉ B2B mới chờ xác nhận",
                Content = $"Doanh nghiệp {request.CompanyName} (MST: {request.TaxCode}) vừa đặt đơn hàng buôn {orderCode} gồm {request.Quantity:N0} {product.Unit} {product.ProductName} (Tổng giá trị: {finalAmount:N0} ₫). Vui lòng kiểm tra và xác nhận đơn hàng.",
                NotificationType = "FarmerOrderUpdate",
                DeliveryChannel = "Email",
                DeliveryStatus = "Pending",
                SentAt = DateTime.UtcNow,
                RelatedRecordType = "Order",
                RelatedRecordId = order.OrderId
            }, cancellationToken);
        }

        await _repository.AddNotificationAsync(new Notification
        {
            UserId = buyerUserId,
            Title = "Khởi tạo đơn hàng sỉ thành công",
            Content = $"Đơn hàng sỉ {orderCode} ({request.Quantity:N0} {product.Unit} {product.ProductName}) đã được tạo thành công và đang chờ nông trại {product.FarmerProfile?.FarmName} xác nhận.",
            NotificationType = "FarmerOrderUpdate",
            DeliveryChannel = "InApp",
            DeliveryStatus = "Sent",
            SentAt = DateTime.UtcNow,
            RelatedRecordType = "Order",
            RelatedRecordId = order.OrderId
        }, cancellationToken);

        var confirmation = new WholesaleOrderConfirmationDto(
            OrderId: order.OrderId,
            OrderCode: order.OrderCode,
            SellerOrderCode: sellerOrder.SellerOrderCode,
            OrderStatus: "PendingConfirmation",
            OrderDate: order.OrderDate,
            ExpectedDeliveryDate: order.ExpectedDeliveryDate,
            ProductId: product.ProductId,
            ProductName: product.ProductName,
            Quantity: request.Quantity,
            Unit: product.Unit,
            UnitPrice: unitPrice,
            Subtotal: subtotal,
            VatAmount: vatAmount,
            DeliveryFee: logisticsFee,
            FinalAmount: finalAmount,
            RecipientName: order.RecipientNameSnapshot,
            RecipientPhone: order.RecipientPhoneSnapshot,
            ShippingAddress: order.ShippingAddress,
            CompanyName: request.CompanyName.Trim(),
            TaxCode: request.TaxCode.Trim(),
            PaymentMethod: paymentMethod,
            BatchAllocations: batchAllocations,
            Message: "Đơn hàng sỉ B2B đã được khởi tạo thành công và đang chờ thanh toán SePay (giữ hàng 15 phút).",
            ReservationExpiresAt: order.ReservationExpiresAt,
            VoucherDiscount: voucherDiscount,
            VoucherCode: appliedVoucherCode);

        return Result<WholesaleOrderConfirmationDto>.Ok(confirmation);
    }

    public async Task<Result<WholesaleBuyerOrdersOverviewDto>> GetBuyerWholesaleOrdersOverviewAsync(
        long buyerUserId,
        CancellationToken cancellationToken)
    {
        var orders = await _repository.GetBuyerWholesaleOrdersAsync(buyerUserId, cancellationToken);
        var nowUtc = DateTime.UtcNow;

        // Tự động hủy các đơn sỉ quá thời gian giữ hàng thanh toán (ReservationExpiresAt <= nowUtc)
        foreach (var o in orders)
        {
            if (IsOrderExpiredAndUnpaid(o, nowUtc))
            {
                await CancelExpiredWholesaleOrderAsync(o.OrderCode, cancellationToken);
            }
        }

        // Nạp lại danh sách sau khi đã cập nhật các đơn quá hạn (nếu có)
        if (orders.Any(o => IsOrderExpiredAndUnpaid(o, nowUtc)))
        {
            orders = await _repository.GetBuyerWholesaleOrdersAsync(buyerUserId, cancellationToken);
        }

        var buyerKyc = await _repository.GetBuyerKycProfileAsync(buyerUserId, cancellationToken);

        var orderDtos = orders
            .Select(o => MapOrderToListItemDto(o, buyerKyc))
            .ToList();

        var activeOrderDtos = orderDtos
            .Where(o => !string.Equals(o.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var supplierBreakdown = activeOrderDtos
            .GroupBy(o => new { o.FarmerProfileId, o.SellerName })
            .Select(g => new WholesaleSupplierSummaryDto(
                FarmerProfileId: g.Key.FarmerProfileId,
                SellerName: g.Key.SellerName,
                FarmAddress: g.Select(x => x.FarmAddress).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? "Việt Nam",
                OrderCount: g.Count(),
                TotalQuantity: g.Sum(x => x.Quantity),
                Unit: g.Select(x => x.Unit).FirstOrDefault() ?? "kg",
                TotalSpendAmount: g.Sum(x => x.FinalAmount),
                LatestOrderDate: g.Max(x => x.OrderDate),
                ProductsBought: g.Select(x => x.ProductName).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList()))
            .OrderByDescending(s => s.OrderCount)
            .ThenByDescending(s => s.TotalSpendAmount)
            .ToList();

        var paidCount = orderDtos.Count(o => string.Equals(o.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase));
        var pendingCount = orderDtos.Count(o => string.Equals(o.PaymentStatus, "PendingPayment", StringComparison.OrdinalIgnoreCase));

        var overview = new WholesaleBuyerOrdersOverviewDto(
            TotalOrdersCount: activeOrderDtos.Count,
            PaidOrdersCount: paidCount,
            PendingOrdersCount: pendingCount,
            TotalSuppliersCount: supplierBreakdown.Count,
            TotalVolume: activeOrderDtos.Sum(o => o.Quantity),
            TotalSpendAmount: activeOrderDtos.Sum(o => o.FinalAmount),
            SupplierBreakdown: supplierBreakdown,
            Orders: orderDtos);

        return Result<WholesaleBuyerOrdersOverviewDto>.Ok(overview);
    }

    public async Task<Result<WholesaleOrderListItemDto>> GetWholesaleOrderDetailByCodeAsync(
        string orderCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            return Result<WholesaleOrderListItemDto>.Fail("Mã đơn hàng không hợp lệ.", 400);
        }

        var cleanCode = orderCode.Trim().TrimStart('#');
        var order = await _repository.GetOrderByCodeAsync(cleanCode, cancellationToken);
        if (order == null)
        {
            return Result<WholesaleOrderListItemDto>.Fail($"Không tìm thấy đơn hàng sỉ #{cleanCode}.", 404);
        }

        if (IsOrderExpiredAndUnpaid(order, DateTime.UtcNow))
        {
            return await CancelExpiredWholesaleOrderAsync(cleanCode, cancellationToken);
        }

        var buyerKyc = await _repository.GetBuyerKycProfileAsync(order.BuyerUserId, cancellationToken);
        return Result<WholesaleOrderListItemDto>.Ok(MapOrderToListItemDto(order, buyerKyc));
    }

    public async Task<Result<WholesaleOrderListItemDto>> CancelExpiredWholesaleOrderAsync(
        string orderCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            return Result<WholesaleOrderListItemDto>.Fail("Mã đơn hàng không hợp lệ.", 400);
        }

        var cleanCode = orderCode.Trim().TrimStart('#');
        var order = await _repository.GetWholesaleOrderForPaymentAsync(cleanCode, cancellationToken);
        if (order == null)
        {
            return Result<WholesaleOrderListItemDto>.Fail($"Không tìm thấy đơn hàng sỉ #{cleanCode}.", 404);
        }

        var alreadyPaid = string.Equals(order.OrderStatus, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(order.OrderStatus, "Completed", StringComparison.OrdinalIgnoreCase) ||
                          order.PaymentTransactions.Any(p => string.Equals(p.PaymentStatus, "Success", StringComparison.OrdinalIgnoreCase));

        if (!alreadyPaid && !string.Equals(order.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            var now = DateTime.UtcNow;
            order.OrderStatus = "Cancelled";

            foreach (var sellerOrder in order.SellerOrders)
            {
                sellerOrder.Status = "Cancelled";
                foreach (var item in sellerOrder.OrderItems)
                {
                    item.ItemStatus = "Cancelled";
                    foreach (var alloc in item.OrderItemBatchAllocations.Where(a => a.AllocationStatus == "Reserved"))
                    {
                        alloc.HarvestBatch?.ReleaseReservation(alloc.AllocatedQuantity);
                        alloc.AllocationStatus = "Released";
                        alloc.ReleasedAt = now;
                    }
                }
            }

            foreach (var redemption in order.VoucherRedemptions.Where(r => r.Status == "Reserved"))
            {
                redemption.Status = "Released";
                redemption.ReleasedAt = now;
            }

            await _repository.SaveChangesAsync(cancellationToken);

            await _repository.AddNotificationAsync(new Notification
            {
                UserId = order.BuyerUserId,
                Title = "Đơn hàng sỉ tự động hủy do hết hạn thanh toán",
                Content = $"Đơn hàng sỉ #{order.OrderCode} ({order.FinalAmount:N0} ₫) đã tự động bị hủy và hoàn trả tồn kho lô hàng do hết thời gian thanh toán SePay.",
                NotificationType = "FarmerOrderUpdate",
                DeliveryChannel = "InApp",
                DeliveryStatus = "Sent",
                SentAt = now,
                RelatedRecordType = "Order",
                RelatedRecordId = order.OrderId
            }, cancellationToken);
        }

        var refreshed = await _repository.GetOrderByCodeAsync(cleanCode, cancellationToken) ?? order;
        var buyerKyc = await _repository.GetBuyerKycProfileAsync(refreshed.BuyerUserId, cancellationToken);
        return Result<WholesaleOrderListItemDto>.Ok(MapOrderToListItemDto(refreshed, buyerKyc));
    }

    private static bool IsOrderExpiredAndUnpaid(Order order, DateTime nowUtc)
    {
        if (!order.ReservationExpiresAt.HasValue || order.ReservationExpiresAt.Value > nowUtc)
        {
            return false;
        }

        if (string.Equals(order.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.OrderStatus, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.OrderStatus, "Preparing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.OrderStatus, "ReadyToShip", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.OrderStatus, "Shipping", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.OrderStatus, "Delivered", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(order.OrderStatus, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hasSuccessPayment = order.PaymentTransactions.Any(p =>
            string.Equals(p.PaymentStatus, "Success", StringComparison.OrdinalIgnoreCase));

        return !hasSuccessPayment;
    }

    public async Task<Result<WholesaleOrderListItemDto>> ConfirmSePayWholesaleOrderAsync(
        string orderCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            return Result<WholesaleOrderListItemDto>.Fail("Mã đơn hàng không hợp lệ.", 400);
        }

        var cleanCode = orderCode.Trim().TrimStart('#');
        var order = await _repository.GetWholesaleOrderForPaymentAsync(cleanCode, cancellationToken);
        if (order == null)
        {
            return Result<WholesaleOrderListItemDto>.Fail($"Không tìm thấy đơn hàng sỉ #{cleanCode}.", 404);
        }

        var now = DateTime.UtcNow;
        var alreadyPaid = string.Equals(order.OrderStatus, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(order.OrderStatus, "Completed", StringComparison.OrdinalIgnoreCase) ||
                          order.PaymentTransactions.Any(p => string.Equals(p.PaymentStatus, "Success", StringComparison.OrdinalIgnoreCase));

        if (!alreadyPaid)
        {
            order.OrderStatus = "Confirmed";
            foreach (var sellerOrder in order.SellerOrders)
            {
                sellerOrder.Status = "Confirmed";
            }

            var txCode = $"SEPAY-WS-{order.OrderId}-{Random.Shared.Next(1000, 9999)}";
            await _repository.AddPaymentTransactionAsync(new PaymentTransaction
            {
                OrderId = order.OrderId,
                TransactionCode = txCode,
                GatewayProvider = "SePay",
                Amount = order.FinalAmount,
                PaymentType = "Transfer",
                PaymentStatus = "Success",
                PaidAt = now,
                PaymentPurpose = "RetailOrderPayment",
                RequestedAt = now,
                RequestKey = Guid.NewGuid().ToString("N"),
                ProviderTransactionReference = cleanCode.Replace("-", "")
            }, cancellationToken);

            await _repository.AddNotificationAsync(new Notification
            {
                UserId = order.BuyerUserId,
                Title = "Thanh toán đơn hàng sỉ SePay thành công",
                Content = $"Đơn hàng sỉ #{order.OrderCode} ({order.FinalAmount:N0} ₫) đã được xác nhận thanh toán qua chuyển khoản QR SePay.",
                NotificationType = "OrderPayment",
                DeliveryChannel = "InApp",
                DeliveryStatus = "Sent",
                SentAt = now,
                RelatedRecordType = "Order",
                RelatedRecordId = order.OrderId
            }, cancellationToken);
        }

        var refreshed = await _repository.GetOrderByCodeAsync(cleanCode, cancellationToken) ?? order;
        var buyerKyc = await _repository.GetBuyerKycProfileAsync(refreshed.BuyerUserId, cancellationToken);
        return Result<WholesaleOrderListItemDto>.Ok(MapOrderToListItemDto(refreshed, buyerKyc));
    }

    private static WholesaleOrderListItemDto MapOrderToListItemDto(Order order, WholesaleBuyerProfile? buyerKyc)
    {
        var sellerOrder = order.SellerOrders.FirstOrDefault();
        var orderItem = sellerOrder?.OrderItems.FirstOrDefault();
        var farmerProfile = sellerOrder?.FarmerProfile;

        var sellerName = !string.IsNullOrWhiteSpace(sellerOrder?.SellerNameSnapshot)
            ? sellerOrder!.SellerNameSnapshot
            : (farmerProfile?.FarmName ?? "Nông Trại Đối Tác B2B");
        var farmAddress = farmerProfile?.Address ?? "Việt Nam";

        var companyName = buyerKyc?.CompanyName ?? "Công Ty Thực Phẩm Xanh Á Châu";
        var taxCode = buyerKyc?.TaxCode ?? "0101234561";
        var rawNotes = order.OrderNotes ?? string.Empty;
        var cleanNotes = rawNotes;

        // Parse "[Hóa đơn DN: {Company} - MST: {TaxCode}] {Notes}" if present
        if (rawNotes.StartsWith("[Hóa đơn DN:", StringComparison.OrdinalIgnoreCase))
        {
            var closeIdx = rawNotes.IndexOf(']');
            if (closeIdx > 0)
            {
                var headerPart = rawNotes[12..closeIdx].Trim();
                var mstIdx = headerPart.IndexOf("- MST:", StringComparison.OrdinalIgnoreCase);
                if (mstIdx > 0)
                {
                    companyName = headerPart[..mstIdx].Trim();
                    taxCode = headerPart[(mstIdx + 6)..].Trim();
                }
                cleanNotes = rawNotes[(closeIdx + 1)..].Trim();
            }
        }

        var isPaid = string.Equals(order.OrderStatus, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(order.OrderStatus, "Shipping", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(order.OrderStatus, "Delivered", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(order.OrderStatus, "Completed", StringComparison.OrdinalIgnoreCase) ||
                     order.PaymentTransactions.Any(p => string.Equals(p.PaymentStatus, "Success", StringComparison.OrdinalIgnoreCase));

        var isCancelled = !isPaid && (
            string.Equals(order.OrderStatus, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            order.SellerOrders.Any(so => string.Equals(so.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)));

        var paymentStatus = isPaid ? "Paid" : (isCancelled ? "Cancelled" : "PendingPayment");

        var batchAllocations = (orderItem?.OrderItemBatchAllocations ?? [])
            .Select(ba => new WholesaleBatchAllocationDto(
                HarvestBatchId: ba.HarvestBatchId,
                BatchCode: ba.HarvestBatch?.BatchCode ?? $"BATCH-{ba.HarvestBatchId}",
                AllocatedQuantity: ba.AllocatedQuantity,
                Unit: orderItem?.UnitSnapshot ?? "kg"))
            .ToList();

        var primaryImg = orderItem?.Product?.ProductImages?.FirstOrDefault(i => i.IsPrimary)?.ImageUrl
                         ?? orderItem?.Product?.ProductImages?.FirstOrDefault()?.ImageUrl;

        var transferContent = new string((order.OrderCode ?? string.Empty)
            .Where(char.IsLetterOrDigit)
            .ToArray())
            .ToUpperInvariant();
        if (transferContent.Length > 18)
        {
            transferContent = transferContent[..18];
        }

        return new WholesaleOrderListItemDto(
            OrderId: order.OrderId,
            OrderCode: order.OrderCode ?? $"ORD-{order.OrderId}",
            SellerOrderCode: sellerOrder?.SellerOrderCode ?? $"{order.OrderCode}-1",
            OrderStatus: isCancelled ? "Cancelled" : order.OrderStatus,
            PaymentStatus: paymentStatus,
            OrderDate: order.OrderDate,
            ExpectedDeliveryDate: order.ExpectedDeliveryDate,
            FarmerProfileId: sellerOrder?.FarmerProfileId ?? 0,
            SellerName: sellerName,
            FarmAddress: farmAddress,
            ProductId: orderItem?.ProductId ?? 0,
            ProductName: orderItem?.ProductNameSnapshot ?? "Nông sản sỉ B2B",
            ProductImageUrl: primaryImg,
            Quantity: orderItem?.Quantity ?? 0m,
            Unit: orderItem?.UnitSnapshot ?? "kg",
            UnitPrice: orderItem?.UnitPriceSnapshot ?? 0m,
            Subtotal: order.TotalAmount,
            DeliveryFee: order.DeliveryFee,
            FinalAmount: order.FinalAmount,
            RecipientName: order.RecipientNameSnapshot,
            RecipientPhone: order.RecipientPhoneSnapshot,
            ShippingAddress: order.ShippingAddress,
            CompanyName: companyName,
            TaxCode: taxCode,
            PaymentMethod: string.IsNullOrWhiteSpace(order.PaymentMethod) ? "SePay" : order.PaymentMethod,
            OrderNotes: string.IsNullOrWhiteSpace(cleanNotes) ? null : cleanNotes,
            TransferContent: transferContent,
            BatchAllocations: batchAllocations,
            ReservationExpiresAt: order.ReservationExpiresAt);
    }

    private static (string TierName, decimal DiscountPercent, decimal UnitPrice) DetermineTier(
        decimal quantity,
        decimal moq,
        decimal basePrice)
    {
        if (quantity >= 5 * moq)
        {
            const decimal disc = 8m;
            var price = Math.Round(basePrice * (1 - disc / 100m), 0);
            return ("Khối lượng lớn (Tier 3)", disc, price);
        }

        if (quantity >= 2 * moq)
        {
            const decimal disc = 4m;
            var price = Math.Round(basePrice * (1 - disc / 100m), 0);
            return ("Số lượng vừa (Tier 2)", disc, price);
        }

        return ("Tiêu chuẩn MOQ (Tier 1)", 0m, basePrice);
    }

    private static IReadOnlyList<WholesalePriceTierDto> BuildPriceTiers(decimal moq, decimal baseWholesalePrice)
    {
        return
        [
            new WholesalePriceTierDto(
                TierName: "Tiêu chuẩn MOQ (Tier 1)",
                MinQuantity: moq,
                MaxQuantity: 2 * moq,
                DiscountPercent: 0m,
                UnitPrice: baseWholesalePrice,
                Description: $"Đặt mua từ {moq:N0} đến dưới {2 * moq:N0} đơn vị sản phẩm."),
            new WholesalePriceTierDto(
                TierName: "Số lượng vừa (Tier 2)",
                MinQuantity: 2 * moq,
                MaxQuantity: 5 * moq,
                DiscountPercent: 4m,
                UnitPrice: Math.Round(baseWholesalePrice * 0.96m, 0),
                Description: $"Đặt mua từ {2 * moq:N0} đến dưới {5 * moq:N0} đơn vị sản phẩm (Chiết khấu thêm 4%)."),
            new WholesalePriceTierDto(
                TierName: "Khối lượng lớn (Tier 3)",
                MinQuantity: 5 * moq,
                MaxQuantity: null,
                DiscountPercent: 8m,
                UnitPrice: Math.Round(baseWholesalePrice * 0.92m, 0),
                Description: $"Đặt mua từ {5 * moq:N0} đơn vị sản phẩm trở lên (Chiết khấu cao nhất 8%).")
        ];
    }

    private async Task<decimal> ResolveLogisticsFeeAsync(
        Product product,
        decimal quantity,
        decimal subtotal,
        long? buyerUserId,
        string? provinceCode,
        string? districtCode,
        string? deliveryAddress,
        string? deliveryMethod,
        CancellationToken cancellationToken)
    {
        if (_shippingRateProvider == null || quantity <= 0)
        {
            return CalculateLogisticsFee(quantity);
        }

        try
        {
            var (recipientProvCode, recipientDistCode) = await ResolveGoshipRecipientCodesAsync(
                buyerUserId, provinceCode, districtCode, deliveryAddress, cancellationToken);

            var pickup = product.FarmerProfile is { HasPickupAddress: true }
                ? new ShippingPickup(product.FarmerProfile.PickupProvinceCode!, product.FarmerProfile.PickupDistrictCode!)
                : (_shippingRateProvider.HasPickupAddress(product.FarmerProfileId, null)
                    ? null
                    : new ShippingPickup("100000", "100100"));

            var (normalizedUnit, normalizedQty) = NormalizeShippingUnit(product.Unit, quantity);
            var lines = new List<ShippingPackageLine> { new(normalizedUnit, normalizedQty) };
            var method = string.IsNullOrWhiteSpace(deliveryMethod) ? "Standard" : deliveryMethod.Trim();

            var recipient = new CheckoutAddress(
                RecipientName: "Wholesale Buyer",
                Phone: "0900000000",
                ProvinceCode: recipientProvCode,
                Province: string.Empty,
                DistrictCode: recipientDistCode,
                District: string.Empty,
                WardCode: string.Empty,
                Ward: string.Empty,
                Detail: deliveryAddress ?? string.Empty);

            var rateResult = await _shippingRateProvider.GetRateAsync(
                new ShippingRateRequest(
                    FarmerProfileId: product.FarmerProfileId,
                    Recipient: recipient,
                    Lines: lines,
                    DeclaredValue: Math.Max(subtotal, 1000m),
                    DeliveryMethod: method,
                    Pickup: pickup),
                cancellationToken);

            // Goship SANDBOX only prices parcels sent from Ha Noi (100000/100100) or TP.HCM (700000/700100).
            // When a farm's DB profile has a provincial pickup code (e.g. An Giang 880000, Lam Dong 670000),
            // Goship Sandbox returns 0 rates (IsSupported = false). Retry with the configured sandbox pickup.
            if (rateResult is not { IsSupported: true, Fee: >= 0 })
            {
                var sandboxPickup = new ShippingPickup("100000", "100100");
                rateResult = await _shippingRateProvider.GetRateAsync(
                    new ShippingRateRequest(
                        FarmerProfileId: product.FarmerProfileId,
                        Recipient: recipient,
                        Lines: lines,
                        DeclaredValue: Math.Max(subtotal, 1000m),
                        DeliveryMethod: method,
                        Pickup: sandboxPickup),
                    cancellationToken);
            }

            if (rateResult is { IsSupported: true, Fee: >= 0 })
            {
                return Math.Round(rateResult.Fee.Value, 0);
            }
        }
        catch
        {
            // Fallback to tier formula if external Goship call fails
        }

        return CalculateLogisticsFee(quantity);
    }

    private async Task<(string ProvinceCode, string DistrictCode)> ResolveGoshipRecipientCodesAsync(
        long? buyerUserId,
        string? provinceCode,
        string? districtCode,
        string? deliveryAddress,
        CancellationToken cancellationToken)
    {
        string? addrText = deliveryAddress;
        string? provCode = provinceCode?.Trim();
        string? distCode = districtCode?.Trim();

        if (buyerUserId is > 0 && (string.IsNullOrWhiteSpace(addrText) || !IsGoshipCode(provCode) || !IsGoshipCode(distCode)))
        {
            var buyerUser = await _repository.GetUserByIdAsync(buyerUserId.Value, cancellationToken);
            var buyerKyc = await _repository.GetBuyerKycProfileAsync(buyerUserId.Value, cancellationToken);
            var savedAddr = buyerUser?.DeliveryAddress;

            if (savedAddr != null)
            {
                if (string.IsNullOrWhiteSpace(provCode)) provCode = savedAddr.ProvinceCode;
                if (string.IsNullOrWhiteSpace(distCode)) distCode = savedAddr.DistrictCode;
                if (string.IsNullOrWhiteSpace(addrText))
                {
                    addrText = string.Join(", ", new[] { savedAddr.AddressDetail, savedAddr.Ward, savedAddr.District, savedAddr.ProvinceCity }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                }
            }

            if (string.IsNullOrWhiteSpace(addrText) && !string.IsNullOrWhiteSpace(buyerKyc?.BusinessAddress))
            {
                addrText = buyerKyc.BusinessAddress;
            }
        }

        if (IsGoshipCode(provCode) && IsGoshipCode(distCode))
        {
            return (provCode!, distCode!);
        }

        // Try matching via GoshipAddressCatalog if available
        if (_addressCatalog != null && !string.IsNullOrWhiteSpace(addrText))
        {
            var provinces = await _addressCatalog.GetProvincesAsync(cancellationToken);
            if (provinces.Count > 0)
            {
                var normText = NormalizePlaceText(addrText);
                var matchedProv = provinces.FirstOrDefault(p =>
                    normText.Contains(NormalizePlaceText(p.Name), StringComparison.OrdinalIgnoreCase));

                if (matchedProv == null)
                {
                    if (normText.Contains("hcm") || normText.Contains("ho chi minh") || normText.Contains("sai gon"))
                        matchedProv = provinces.FirstOrDefault(p => p.Code == "700000" || NormalizePlaceText(p.Name).Contains("ho chi minh"));
                    else if (normText.Contains("ha noi") || (provCode != null && provCode.StartsWith("HN", StringComparison.OrdinalIgnoreCase)))
                        matchedProv = provinces.FirstOrDefault(p => p.Code == "100000" || NormalizePlaceText(p.Name).Contains("ha noi"));
                }

                if (matchedProv != null)
                {
                    var districts = await _addressCatalog.GetDistrictsAsync(matchedProv.Code, cancellationToken);
                    if (districts.Count > 0)
                    {
                        var matchedDist = districts.FirstOrDefault(d =>
                            normText.Contains(NormalizePlaceText(d.Name), StringComparison.OrdinalIgnoreCase))
                            ?? districts[0];
                        return (matchedProv.Code, matchedDist.Code);
                    }
                }
            }
        }

        // Built-in Goship v2 province/district fallback mapping
        var combined = NormalizePlaceText($"{provCode} {addrText}");
        if (combined.Contains("ha noi") || combined.Contains("hn-01") || combined.Contains("thanh xuan") || combined.Contains("ba dinh") || combined.Contains("cau giay"))
            return ("100000", "100100");
        if (combined.Contains("da nang") || combined.Contains("dn-48") || combined.Contains("hai chau"))
            return ("550000", "550100");
        if (combined.Contains("hai phong") || combined.Contains("hp-31") || combined.Contains("ngo quyen"))
            return ("180000", "180100");
        if (combined.Contains("can tho") || combined.Contains("ct-92") || combined.Contains("ninh kieu"))
            return ("900000", "900100");

        // Default to TP. Hồ Chí Minh (700000 / 700100)
        return ("700000", "700100");
    }

    private static bool IsGoshipCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && code.Length >= 4 && code.All(char.IsDigit);

    private static (string Unit, decimal Quantity) NormalizeShippingUnit(string? unit, decimal quantity)
    {
        var u = (unit ?? "kg").Trim().ToLowerInvariant();
        return u switch
        {
            "g" or "gram" or "grams" => ("g", quantity),
            "kg" or "kilogram" or "kilograms" => ("kg", quantity),
            "yến" or "yen" => ("kg", quantity * 10m),
            "tạ" or "ta" => ("kg", quantity * 100m),
            "tấn" or "tan" => ("kg", quantity * 1000m),
            _ => ("kg", quantity)
        };
    }

    private static string NormalizePlaceText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var decomposed = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch switch
                {
                    'đ' => 'd',
                    'Đ' => 'd',
                    _ => char.ToLowerInvariant(ch)
                });
            }
        }
        return sb.ToString()
            .Replace("thanh pho ", "")
            .Replace("tp.", "")
            .Replace("tp ", "")
            .Replace("tinh ", "")
            .Replace("quan ", "")
            .Replace("huyen ", "")
            .Trim();
    }

    private async Task<(Voucher? Voucher, decimal Discount, string? AppliedCode, string? Warning)> EvaluateVoucherAsync(
        string? voucherCode,
        decimal subtotal,
        long farmerProfileId,
        long buyerUserId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voucherCode))
        {
            return (null, 0m, null, null);
        }

        var code = voucherCode.Trim();
        var voucher = await _repository.GetVoucherByCodeAsync(code, cancellationToken);
        var nowUtc = DateTime.UtcNow;

        if (voucher == null ||
            !voucher.IsActive ||
            voucher.StartDate > nowUtc ||
            voucher.EndDate <= nowUtc ||
            subtotal <= 0 ||
            voucher.DiscountValue <= 0 ||
            voucher.DiscountType is not ("Percentage" or "FixedAmount") ||
            (voucher.DiscountType == "Percentage" && voucher.DiscountValue > 100) ||
            (voucher.FarmerProfileId.HasValue && voucher.FarmerProfileId.Value != farmerProfileId))
        {
            return (null, 0m, null, "MSG116: Voucher không hợp lệ hoặc không áp dụng cho nông trại này; đã bỏ giảm giá.");
        }

        if (subtotal < voucher.MinOrderAmount)
        {
            return (null, 0m, null, $"Đơn hàng chưa đạt giá trị tối thiểu {voucher.MinOrderAmount:N0} ₫ để áp dụng mã {voucher.VoucherCode}.");
        }

        var reserved = await _repository.GetReservedVoucherCountAsync(voucher.VoucherId, cancellationToken);
        if (voucher.UsageLimit.HasValue && voucher.UsedCount + reserved >= voucher.UsageLimit.Value)
        {
            return (null, 0m, null, $"Mã voucher {voucher.VoucherCode} đã hết lượt sử dụng.");
        }

        var customerUses = await _repository.GetCustomerVoucherUsesAsync(buyerUserId, cancellationToken);
        if (customerUses.GetValueOrDefault(voucher.VoucherId) >= voucher.PerCustomerLimit)
        {
            return (null, 0m, null, $"Bạn đã sử dụng hết lượt áp dụng cho mã voucher {voucher.VoucherCode}.");
        }

        var rawDiscount = voucher.DiscountType == "Percentage"
            ? Math.Round(subtotal * voucher.DiscountValue / 100m, 0)
            : Math.Round(voucher.DiscountValue, 0);

        var discount = Math.Min(subtotal, Math.Min(rawDiscount, voucher.MaxDiscountAmount ?? subtotal));
        return (voucher, discount, voucher.VoucherCode, null);
    }

    private static decimal CalculateLogisticsFee(decimal quantity)
    {
        if (quantity <= 0) return 0m;
        var blocksOf100Kg = Math.Ceiling(quantity / 100m);
        return Math.Max(150_000m, blocksOf100Kg * 125_000m);
    }
}

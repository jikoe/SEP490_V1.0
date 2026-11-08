using System.Text.Json.Serialization;

namespace AgriSustain.Application.DTOs.B2B;

public sealed record WholesaleProductInfoDto(
    long ProductId,
    string ProductName,
    string CategoryName,
    string Unit,
    decimal BaseRetailPrice,
    decimal WholesalePrice,
    decimal WholesaleMinQuantity,
    decimal AvailableStock,
    string? PrimaryImageUrl,
    IReadOnlyList<string> Certifications,
    long FarmerProfileId,
    string FarmName,
    string RepresentativeName,
    string FarmAddress,
    string? FarmAvatarUrl,
    IReadOnlyList<WholesalePriceTierDto> PriceTiers,
    WholesaleBuyerCorporateKycDto BuyerKyc,
    IReadOnlyList<WholesaleAvailableBatchDto>? AvailableBatches = null)
{
    public decimal MinQuantity => WholesaleMinQuantity;
    public string FarmerFarmName => FarmName;
    public string FarmerName => RepresentativeName;
    public string FarmerAddress => FarmAddress;
}

public sealed record WholesaleAvailableBatchDto(
    long HarvestBatchId,
    string BatchCode,
    decimal AvailableQuantity,
    string? ActualHarvestDate,
    string Status);

public sealed record WholesalePriceTierDto(
    string TierName,
    decimal MinQuantity,
    decimal? MaxQuantity,
    decimal DiscountPercent,
    decimal UnitPrice,
    string Description);

public sealed record WholesaleBuyerCorporateKycDto(
    long UserId,
    string CompanyName,
    string TaxCode,
    string RepresentativeName,
    string BusinessAddress,
    string? PhoneNumber,
    string VerificationStatus,
    bool IsApprovedKyc);

public sealed record WholesalePriceCalculationRequest(
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    long ProductId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal Quantity);

public sealed record WholesalePriceCalculationResponse(
    decimal Quantity,
    decimal BaseRetailPrice,
    decimal WholesaleBasePrice,
    decimal AppliedUnitPrice,
    string ActiveTierName,
    decimal TierDiscountPercent,
    decimal LineSubtotal,
    decimal TotalSavings,
    decimal VatRate,
    decimal VatAmount,
    decimal LogisticsFee,
    decimal FinalTotal,
    bool IsValidQuantity,
    string? ErrorMessage)
{
    public decimal TotalAmount => FinalTotal;
}

public sealed record CreateWholesaleOrderRequest(
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    long ProductId,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    decimal Quantity,
    string? WarehouseAddress = null,
    string? ProvinceCode = null,
    string? ProvinceName = null,
    string? DistrictCode = null,
    string? DistrictName = null,
    string? WardCode = null,
    string? WardName = null,
    string RecipientName = "",
    string RecipientPhone = "",
    string CompanyName = "",
    string TaxCode = "",
    DateTime? ExpectedDeliveryDate = null,
    string? PaymentMethod = null,
    string? OrderNotes = null,
    string? RequestKey = null,
    string? DeliveryAddress = null,
    string? PaymentTerms = null,
    string? Notes = null);

public sealed record WholesaleOrderConfirmationDto(
    long OrderId,
    string OrderCode,
    string SellerOrderCode,
    string OrderStatus,
    DateTime OrderDate,
    DateTime? ExpectedDeliveryDate,
    long ProductId,
    string ProductName,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal Subtotal,
    decimal VatAmount,
    decimal DeliveryFee,
    decimal FinalAmount,
    string RecipientName,
    string RecipientPhone,
    string ShippingAddress,
    string CompanyName,
    string TaxCode,
    string PaymentMethod,
    IReadOnlyList<WholesaleBatchAllocationDto> BatchAllocations,
    string Message)
{
    public decimal TotalQuantity => Quantity;
    public decimal TotalAmount => FinalAmount;
    public string PaymentTerms => PaymentMethod;
}

public sealed record WholesaleBatchAllocationDto(
    long HarvestBatchId,
    string BatchCode,
    decimal AllocatedQuantity,
    string Unit);

public sealed record WholesaleOrderListItemDto(
    long OrderId,
    string OrderCode,
    string SellerOrderCode,
    string OrderStatus,
    string PaymentStatus,
    DateTime OrderDate,
    DateTime? ExpectedDeliveryDate,
    long FarmerProfileId,
    string SellerName,
    string FarmAddress,
    long ProductId,
    string ProductName,
    string? ProductImageUrl,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal FinalAmount,
    string RecipientName,
    string RecipientPhone,
    string ShippingAddress,
    string CompanyName,
    string TaxCode,
    string PaymentMethod,
    string? OrderNotes,
    string TransferContent,
    IReadOnlyList<WholesaleBatchAllocationDto> BatchAllocations);

public sealed record WholesaleSupplierSummaryDto(
    long FarmerProfileId,
    string SellerName,
    string FarmAddress,
    int OrderCount,
    decimal TotalQuantity,
    string Unit,
    decimal TotalSpendAmount,
    DateTime LatestOrderDate,
    IReadOnlyList<string> ProductsBought);

public sealed record WholesaleBuyerOrdersOverviewDto(
    int TotalOrdersCount,
    int PaidOrdersCount,
    int PendingOrdersCount,
    int TotalSuppliersCount,
    decimal TotalVolume,
    decimal TotalSpendAmount,
    IReadOnlyList<WholesaleSupplierSummaryDto> SupplierBreakdown,
    IReadOnlyList<WholesaleOrderListItemDto> Orders);


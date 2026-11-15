using System.Text.Json.Serialization;

namespace AgriSustain.Application.DTOs.Offtake;

public sealed record OfftakeProposalRequest(Guid RequestId, long FarmerId, long ProductId,
    decimal Quantity, decimal UnitPrice, DateOnly ValidFrom, DateOnly ValidUntil,
    DateOnly DeliveryDate, string? DeliveryTerms, string? PaymentTerms, decimal DepositAmount)
{
    // Quantity and UnitPrice use the product unit. This field preserves the chosen input unit.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? QuantityUnit { get; init; }
}

public sealed record OfftakeProposalReceipt(long Id, string Code);

public sealed record OfftakeDraft(string Code, int Version, DateTime CreatedAt,
    OfftakeProposalRequest Proposal, OfftakeProduct? Product)
{
    public string? RevisionReason { get; init; }
    public string? RevisionChanges { get; init; }
}

public sealed record OfftakeProduct(long Id, long FarmerId, string Farmer, string Name,
    string Category, string Unit, string? ImageUrl)
{
    public IReadOnlyList<OfftakeQuantityUnit> QuantityUnits => OfftakeQuantityUnit.For(Unit);
}

public sealed record OfftakeQuantityUnit(string Unit, decimal Factor)
{
    private static readonly OfftakeQuantityUnit[] WeightUnits =
        [new("g", 0.001m), new("kg", 1), new("yến", 10), new("tạ", 100), new("tấn", 1000)];
    private static readonly OfftakeQuantityUnit[] VolumeUnits = [new("ml", 0.001m), new("lít", 1)];

    public static IReadOnlyList<OfftakeQuantityUnit> For(string productUnit)
    {
        foreach (var family in new[] { WeightUnits, VolumeUnits })
        {
            var basis = family.FirstOrDefault(choice => string.Equals(choice.Unit, productUnit,
                StringComparison.OrdinalIgnoreCase));
            if (basis is not null)
            {
                return family.Select(choice => choice == basis ? new OfftakeQuantityUnit(productUnit, 1)
                    : new OfftakeQuantityUnit(choice.Unit, choice.Factor / basis.Factor)).ToArray();
            }
        }
        return [new(productUnit, 1)];
    }
}

public sealed record OfftakeCatalog(IReadOnlyList<OfftakeProduct> Products, string? SupportEmail);

public sealed record OfftakeTrackingQuery(string? Keyword = null, string? Status = null,
    string? Product = null, DateOnly? From = null, DateOnly? To = null,
    int Page = 1, int PageSize = 10);

public sealed record OfftakeTrackingPage(IReadOnlyList<OfftakeTrackingRow> Items, int Total,
    int Page, int PageSize, IReadOnlyList<OfftakeStatusCount> StatusCounts);

public sealed record OfftakeStatusCount(string Status, int Count);

public sealed record OfftakeTrackingRow
{
    public long Id { get; init; }
    public required string Code { get; init; }
    public string? Partner { get; init; }
    public string? Product { get; init; }
    public string? ImageUrl { get; init; }
    public decimal? Quantity { get; init; }
    public string? Unit { get; init; }
    public decimal? Value { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateOnly? DeliveryFrom { get; init; }
    public DateOnly? DeliveryTo { get; init; }
    public required string Status { get; init; }
    public bool CanEditRevision { get; init; }
}

public sealed record OfftakeTrackingDetail(string Code, string Status, string? Partner,
    int? Version, string? Terms, string? PaymentTerms, decimal? Deposit, DateOnly? ValidFrom,
    DateOnly? ValidUntil, IReadOnlyList<OfftakeLine> Lines,
    IReadOnlyList<OfftakeDelivery> Milestones, IReadOnlyList<OfftakeShipment> Shipments,
    IReadOnlyList<OfftakeInvoice> Invoices, IReadOnlyList<OfftakeConfirmation> Confirmations)
{
    public OfftakeDocumentDetails? Document { get; init; }
    public bool CanEditRevision { get; init; }
    public string? FeedbackReason { get; init; }
    public string? ProposedChanges { get; init; }
    public IReadOnlyList<OfftakeSeasonProgress> Production { get; init; } = [];
    public IReadOnlyList<OfftakeContractEvent> DeliveryEvents { get; init; } = [];
    public IReadOnlyList<OfftakeDiscrepancy> Discrepancies { get; init; } = [];
}

public sealed record OfftakeSeasonProgress(string Product, string Season, string Status, DateOnly EstimatedHarvest,
    DateOnly? ActualHarvest, decimal Harvested, string Unit, bool BehindSchedule,
    IReadOnlyList<OfftakeProductionLog> Logs);
public sealed record OfftakeProductionLog(DateTime Date, string Activity, string? Description);
public sealed record OfftakeContractEvent(string Action, DateTime At, string? Reason, DateOnly? ProposedDate, decimal? Quantity);
public sealed record OfftakeDiscrepancy(long Id, long? SellerOrderId, string Status, string? Description,
    string? Resolution, IReadOnlyList<OfftakeDiscrepancyReply> Replies, IReadOnlyList<OfftakeEvidence> Evidence);
public sealed record OfftakeDiscrepancyReply(long ShipmentItemId, decimal? Quantity, string Resolution, string? Reason, DateTime At);
public sealed record OfftakeEvidence(long Id, bool FromBuyer, string? Description);
public sealed record OfftakeInspectionLine(long ItemId, decimal Received, decimal Accepted, string Grade);
public sealed record OfftakeInspectionRequest(IReadOnlyList<OfftakeInspectionLine> Items, string? Reason,
    IReadOnlyList<string>? Evidence);
public sealed record OfftakeInspectionResult(long? TicketId);
public sealed record OfftakeDiscrepancyDecision(bool Accept, string? Reason, DateTime? ReplyAt);
public sealed record OfftakeDocumentDetails(OfftakeParty Supplier, OfftakeParty Buyer,
    IReadOnlyList<OfftakeDelivery> DeliverySchedule, DateTime VersionCreatedAt);

public sealed record OfftakeParty(string Name, string Representative, string Address, string? TaxCode);

public sealed record OfftakeLine(string Product, decimal Quantity, string Unit,
    decimal? UnitPrice, string? PriceFormula, string? Quality = null,
    decimal? DisplayQuantity = null, string? DisplayUnit = null);

public sealed record OfftakeDelivery(string Title, DateOnly DueDate, decimal? Quantity,
    decimal Delivered, decimal Accepted, string Status, string PaymentStatus);

public sealed record OfftakeShipment(string? TrackingNumber, DateTime? PlannedAt,
    DateTime? ShippedAt, DateTime? DeliveredAt, string Status)
{
    public long Id { get; init; }
    public long SellerOrderId { get; init; }
    public bool CanInspect { get; init; }
    public IReadOnlyList<OfftakeShipmentLine> Items { get; init; } = [];
}
public sealed record OfftakeShipmentLine(long Id, string Product, string Batch, string Unit, decimal Shipped,
    decimal? Received, decimal? Accepted, string? Grade, string Status);

public sealed record OfftakeInvoice(string Number, DateTime IssuedAt, DateTime? DueAt,
    decimal Amount, string Status)
{
    public long? MilestoneId { get; init; }
    public bool CanPay { get; init; }
}

public sealed record OfftakeConfirmation(string Role, DateTime? ConfirmedAt, string Status,
    string? ConfirmedBy = null);

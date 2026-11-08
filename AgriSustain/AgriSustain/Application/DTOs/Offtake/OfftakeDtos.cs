namespace AgriSustain.Application.DTOs.Offtake;

public sealed record OfftakeProposalRequest(Guid RequestId, long FarmerId, long ProductId,
    decimal Quantity, decimal UnitPrice, DateOnly ValidFrom, DateOnly ValidUntil,
    DateOnly DeliveryDate, string? DeliveryTerms, string? PaymentTerms, decimal DepositAmount);

public sealed record OfftakeProposalReceipt(long Id, string Code);

public sealed record OfftakeDraft(string Code, int Version, DateTime CreatedAt,
    OfftakeProposalRequest Proposal, OfftakeProduct? Product);

public sealed record OfftakeProduct(long Id, long FarmerId, string Farmer, string Name,
    string Category, string Unit, string? ImageUrl);

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
}

public sealed record OfftakeTrackingDetail(string Code, string Status, string? Partner,
    int? Version, string? Terms, string? PaymentTerms, decimal? Deposit, DateOnly? ValidFrom,
    DateOnly? ValidUntil, IReadOnlyList<OfftakeLine> Lines,
    IReadOnlyList<OfftakeDelivery> Milestones, IReadOnlyList<OfftakeShipment> Shipments,
    IReadOnlyList<OfftakeInvoice> Invoices, IReadOnlyList<OfftakeConfirmation> Confirmations,
    bool CanConfirmRevision = false);

public sealed record OfftakeLine(string Product, decimal Quantity, string Unit,
    decimal? UnitPrice, string? PriceFormula);

public sealed record OfftakeDelivery(string Title, DateOnly DueDate, decimal? Quantity,
    decimal Delivered, decimal Accepted, string Status, string PaymentStatus);

public sealed record OfftakeShipment(string? TrackingNumber, DateTime? PlannedAt,
    DateTime? ShippedAt, DateTime? DeliveredAt, string Status);

public sealed record OfftakeInvoice(string Number, DateTime IssuedAt, DateTime? DueAt,
    decimal Amount, string Status);

public sealed record OfftakeConfirmation(string Role, DateTime? ConfirmedAt, string Status);

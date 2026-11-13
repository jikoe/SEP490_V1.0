namespace AgriSustain.Application.DTOs.Payouts;

public sealed class FarmerPayoutQuery
{
    public string? Keyword { get; set; }
    public long? FarmerId { get; set; }
    public string? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
}

public sealed class CreateFarmerPayoutRequest
{
    public long FarmerId { get; set; }
    public List<long> SelectedSourceIds { get; set; } = [];
    public string ExpectedBalance { get; set; } = "";
    public string RequestKey { get; set; } = "";
}

public class PayoutActionRequest
{
    public string ExpectedVersion { get; set; } = "";
    public string RequestKey { get; set; } = "";
}

public sealed class ProcessFarmerPayoutRequest : PayoutActionRequest
{
    public decimal ConfirmedNetAmount { get; set; }
}

public sealed class ReconcileFarmerPayoutRequest
{
    public string ExistingOperationKey { get; set; } = "";
}

public sealed record PayoutSource(long Id, string Reference, string Product, DateTime At,
    decimal Gross, decimal Commission, decimal Deduction, decimal Net,
    decimal CommissionRate, long? CommissionPolicyId, bool Eligible, string? HoldReason,
    decimal Settled, decimal Held, decimal Quantity, decimal UnitPrice);

public sealed record PayoutBank(string BankCode, string BankName, string ShortName,
    string AccountNumber, string AccountHolder, DateTime? VerifiedAt);

public sealed record PayoutPreview(long FarmerId, string FarmerName, PayoutBank? Recipient,
    IReadOnlyList<PayoutSource> Sources, decimal Gross, decimal Commission, decimal Deduction,
    decimal Net, string ExpectedBalance, bool CanCreate, string? Message);

public sealed record PayoutActionResult(long Id, string Status, string Message);

public sealed record PayoutTransferPlan(string OperationKey, string SourceBankCode, string SourceBankName,
    string SourceAccountNumber, string RecipientBankCode, string RecipientAccountNumber,
    decimal Amount, DateTime OpenedAt, string QrUrl);

public sealed record PayoutOutgoingTransfer(string EventId, string Gateway, string SourceAccountNumber,
    decimal Amount, DateTime At, string Content, string? BankReference, string RawPayload);

public sealed record PayoutGatewayLookup(IReadOnlyList<PayoutOutgoingTransfer> Transfers,
    bool LookupAvailable, string? Message);

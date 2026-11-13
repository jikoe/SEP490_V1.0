namespace AgriSustain.Application.DTOs.Offtake;

public sealed class ContractQuery
{
    public string? Keyword { get; set; }
    public string? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int Page { get; set; } = 1;
}

public class ContractActionRequest
{
    public string ExpectedVersion { get; set; } = "";
    public string RequestKey { get; set; } = "";
    public string? Reason { get; set; }
}

public sealed class ContractTermsResponseRequest : ContractActionRequest
{
    public int VersionNo { get; set; }
    public long? CropSeasonId { get; set; }
}

public sealed class AmendContractRequest : ContractActionRequest
{
    public string ProposedChanges { get; set; } = "";
}

public sealed class DispatchContractDeliveryRequest : ContractActionRequest
{
    public long MilestoneId { get; set; }
    public List<ContractBatchQuantity> BatchQuantities { get; set; } = [];
}

public sealed record ContractBatchQuantity(long HarvestBatchId, decimal Quantity);

public sealed class ContractDelayRequest : ContractActionRequest
{
    public long MilestoneId { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly ProposedDate { get; set; }
}

public sealed class ContractDiscrepancyResponseRequest : ContractActionRequest
{
    public long TicketId { get; set; }
    public long ShipmentItemId { get; set; }
    public decimal? ProposedQuantity { get; set; }
    public string ProposedResolution { get; set; } = "";
    public List<string> Evidence { get; set; } = [];
}

public sealed class TerminateContractRequest : ContractActionRequest
{
    public string? SettlementProposal { get; set; }
    public List<string> Evidence { get; set; } = [];
}

public sealed record ContractActionResult(long ContractId, string Status, string Message, string Version);

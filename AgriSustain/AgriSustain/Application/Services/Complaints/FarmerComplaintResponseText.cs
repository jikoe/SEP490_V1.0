using System.Text.Json;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Complaints;

// AuditLog.NewValue retains the full explanation; DisputeEvidence.Description is limited to 500 characters.
public sealed record FarmerComplaintResponseText(long RequestId, string Explanation, string[] EvidenceUrls)
{
    public const string Action = "FarmerComplaintResponse";
    public const string Entity = "DisputeTicket";

    public static FarmerComplaintResponseText? Read(AuditLog audit)
    {
        try
        {
            return JsonSerializer.Deserialize<FarmerComplaintResponseText>(audit.NewValue ?? "null");
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

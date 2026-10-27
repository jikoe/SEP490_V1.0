namespace AgriSustain.Application.Common;

/// <summary>KYC verification states of FarmerProfile / WholesaleBuyerProfile (SRS UC-AUTH-01, UC-AUTH-03).</summary>
public static class VerificationStatuses
{
    public const string NotSubmitted = "NotSubmitted";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    /// <summary>Legacy values written by earlier builds; treated as "waiting for review".</summary>
    public static bool IsPending(string? status) =>
        status is PendingApproval or "Pending" or "PendingVerification";

    public static bool CanSubmit(string? status) => status is NotSubmitted or Rejected;
}

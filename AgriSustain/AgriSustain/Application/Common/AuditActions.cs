namespace AgriSustain.Application.Common;

/// <summary>
/// AuditLog.Action names that business rules read back. The code that writes a row and the code that
/// later searches for it both use these constants, so renaming an action cannot silently break a rule.
/// </summary>
public static class AuditActions
{
    // UC-ADM-01 (BR-DB-03): the pending KYC wait starts at the latest submission.
    /// <summary>A Farmer or Wholesale Buyer submits the KYC dossier for verification.</summary>
    public const string SubmitForVerification = "SubmitForVerification";

    // UC-ADM-01 (BR-DB-03): the pending listing wait starts at the latest of these.
    /// <summary>A Farmer submits a new listing for review.</summary>
    public const string SubmitListing = "SubmitListing";
    /// <summary>A Farmer resubmits a rejected listing.</summary>
    public const string ResubmitListing = "ResubmitListing";
    /// <summary>A Farmer updates a listing in a way that needs a new review.</summary>
    public const string UpdateListing = "UpdateListing";

    // UC-FIN-03 (BR-RF-04): refund history.
    /// <summary>A refund transfer failed; the latest one decides when Transfer Again is allowed.</summary>
    public const string RefundTransferFailed = "RefundTransferFailed";
    /// <summary>Finance Staff reminded the customer to give a refund account; one per 24 hours.</summary>
    public const string RemindRefundAccount = "RemindRefundAccount";
    /// <summary>The customer gave a refund account for the first time.</summary>
    public const string RefundAccountProvided = "RefundAccountProvided";
    /// <summary>The customer changed the refund account.</summary>
    public const string RefundAccountChanged = "RefundAccountChanged";

    // UC-PS-01: platform voucher history; the snapshots keep a deleted voucher's code reserved (BR-PV-01).
    /// <summary>Promotion Staff created a platform voucher.</summary>
    public const string CreateVoucher = "CreateVoucher";
    /// <summary>Promotion Staff edited a platform voucher.</summary>
    public const string UpdateVoucher = "UpdateVoucher";
    /// <summary>Promotion Staff reactivated a platform voucher.</summary>
    public const string ReactivateVoucher = "ReactivateVoucher";
    /// <summary>Promotion Staff deactivated a platform voucher.</summary>
    public const string DeactivateVoucher = "DeactivateVoucher";
    /// <summary>Promotion Staff deleted a platform voucher.</summary>
    public const string DeleteVoucher = "DeleteVoucher";
}

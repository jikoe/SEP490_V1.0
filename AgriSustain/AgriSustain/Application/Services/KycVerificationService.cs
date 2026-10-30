using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Verification;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class KycVerificationService(
    IKycVerificationRepository repository,
    IActorContext actorContext,
    TimeProvider clock) : IKycVerificationService
{
    public const string MSG01 = "Không tìm thấy hồ sơ KYC phù hợp với tiêu chí tìm kiếm.";
    public const string MSG27 = "Phê duyệt hồ sơ KYC thành công. Trạng thái xác minh đã được cập nhật.";
    public const string MSG28 = "Đã từ chối hồ sơ KYC và gửi thông báo cho người dùng.";
    public const string MSG_REQUEST_INFO = "Đã gửi yêu cầu bổ sung thông tin KYC cho người dùng.";

    private async Task<User?> GetAuthorizedOfficerAsync(CancellationToken cancellationToken)
    {
        var actor = await repository.FindActorAsync(actorContext.UserId, cancellationToken);
        if (actor is null || actor.Status != "Active" || actor.IsTemporarilyLocked(clock.GetUtcNow()))
        {
            return null;
        }

        return actor.Role is "VerificationOfficer" or "Verification Officer"
            or "SystemAdmin" or "Administrator" or "Admin"
            ? actor
            : null;
    }

    public async Task<UseCaseResult<Page<KycApplicationSummaryDto>>> SearchAsync(
        KycApplicationSearch search,
        CancellationToken cancellationToken)
    {
        if (await GetAuthorizedOfficerAsync(cancellationToken) is null)
        {
            return Denied<Page<KycApplicationSummaryDto>>();
        }

        var normalized = search with
        {
            Keyword = search.Keyword?.Trim(),
            Role = string.IsNullOrWhiteSpace(search.Role) ? null : search.Role.Trim(),
            Status = string.IsNullOrWhiteSpace(search.Status) ? null : search.Status.Trim(),
            Page = Math.Max(search.Page, 1),
            PageSize = Math.Clamp(search.PageSize, 1, 100)
        };

        return UseCaseResult<Page<KycApplicationSummaryDto>>.Success(
            await repository.SearchAsync(normalized, cancellationToken));
    }

    public async Task<UseCaseResult<KycApplicationDetailDto>> GetDetailAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        if (await GetAuthorizedOfficerAsync(cancellationToken) is null)
        {
            return Denied<KycApplicationDetailDto>();
        }

        var detail = await repository.GetDetailAsync(userId, cancellationToken);
        return detail is null
            ? Missing<KycApplicationDetailDto>()
            : UseCaseResult<KycApplicationDetailDto>.Success(detail);
    }

    public async Task<UseCaseResult<KycReviewResultDto>> ReviewAsync(
        long userId,
        KycReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var officer = await GetAuthorizedOfficerAsync(cancellationToken);
        if (officer is null)
        {
            return Denied<KycReviewResultDto>();
        }

        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("approve" or "reject" or "request-info"))
        {
            return UseCaseResult<KycReviewResultDto>.Failure(
                "INVALID_ACTION",
                "Thao tác thẩm định không hợp lệ. Chỉ chấp nhận approve, reject hoặc request-info.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (action is "reject" or "request-info")
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return UseCaseResult<KycReviewResultDto>.Failure(
                    "REASON_REQUIRED",
                    action == "reject"
                        ? "Vui lòng nhập lý do từ chối hồ sơ KYC."
                        : "Vui lòng nhập nội dung yêu cầu bổ sung thông tin KYC.");
            }

            if (reason.Length > 500)
            {
                return UseCaseResult<KycReviewResultDto>.Failure(
                    "REASON_TOO_LONG",
                    "Lý do hoặc nội dung yêu cầu không được vượt quá 500 ký tự.");
            }
        }
        else if (reason is { Length: > 500 })
        {
            return UseCaseResult<KycReviewResultDto>.Failure(
                "REASON_TOO_LONG",
                "Ghi chú phê duyệt không được vượt quá 500 ký tự.");
        }

        var user = await repository.GetUserForKycReviewAsync(userId, cancellationToken);
        if (user is null || user.Role is not ("Farmer" or "WholesaleBuyer"))
        {
            return Missing<KycReviewResultDto>();
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var targetStatus = action switch
        {
            "approve" => VerificationStatuses.Approved,
            "reject" => VerificationStatuses.Rejected,
            _ => VerificationStatuses.AdditionalInfoRequired
        };

        string previousVerificationStatus = GetCurrentVerificationStatus(user);
        string previousAccountStatus = user.Status;

        if (user.FarmerProfile is not null)
        {
            user.FarmerProfile.VerificationStatus = targetStatus;
            user.FarmerProfile.ApprovedAt = action == "approve" ? now : null;
            user.FarmerProfile.RejectReason = action == "approve" ? null : reason;

            foreach (var certificate in user.FarmerProfile.FarmCertificates)
            {
                certificate.VerificationStatus = targetStatus;
                certificate.ReviewedByUserId = officer.UserId;
                certificate.ReviewedAt = now;
            }
        }
        else if (user.WholesaleBuyerProfile is not null)
        {
            user.WholesaleBuyerProfile.VerificationStatus = targetStatus;
            user.WholesaleBuyerProfile.ApprovedAt = action == "approve" ? now : null;
            user.WholesaleBuyerProfile.RejectReason = action == "approve" ? null : reason;
        }

        if (user.Status != "Suspended")
        {
            if (action == "approve" && user.Status == "Pending")
            {
                user.Status = "Active";
            }
            else if (action == "reject")
            {
                user.Status = "Rejected";
            }
            else if (action == "request-info")
            {
                user.Status = "Pending";
            }
        }

        var auditAction = action switch
        {
            "approve" => "ApproveKyc",
            "reject" => "RejectKyc",
            _ => "RequestKycInfo"
        };

        repository.AddAudit(new AuditLog
        {
            PerformedByUserId = officer.UserId,
            EntityType = "KycApplication",
            EntityId = user.UserId,
            Action = auditAction,
            OldValue = JsonSerializer.Serialize(new
            {
                VerificationStatus = previousVerificationStatus,
                AccountStatus = previousAccountStatus
            }),
            NewValue = JsonSerializer.Serialize(new
            {
                VerificationStatus = targetStatus,
                AccountStatus = user.Status
            }),
            Reason = reason,
            OccurredAt = now,
            TraceId = actorContext.TraceId
        });

        var (notificationTitle, notificationContent, resultMessage) = action switch
        {
            "approve" => (
                "Hồ sơ KYC đã được phê duyệt",
                "Hồ sơ xác minh danh tính / pháp lý (KYC) của bạn đã được Chuyên viên thẩm định phê duyệt. Tài khoản đã sẵn sàng sử dụng các chức năng trên hệ thống.",
                MSG27),
            "reject" => (
                "Hồ sơ KYC bị từ chối",
                $"Hồ sơ xác minh KYC của bạn chưa được chấp thuận. Lý do: {reason}",
                MSG28),
            _ => (
                "Yêu cầu bổ sung thông tin KYC",
                $"Chuyên viên thẩm định yêu cầu bạn bổ sung/chỉnh sửa thông tin KYC: {reason}",
                MSG_REQUEST_INFO)
        };

        repository.AddNotification(new Notification
        {
            UserId = user.UserId,
            Title = notificationTitle,
            Content = notificationContent,
            NotificationType = "KycVerification",
            IsRead = false,
            SentAt = now,
            DeliveryChannel = "InApp",
            DeliveryStatus = "Sent",
            RetryCount = 0,
            RelatedRecordType = "User",
            RelatedRecordId = user.UserId
        });

        await repository.SaveChangesAsync(cancellationToken);

        var effectiveAccountStatus = ComputeEffectiveAccountStatus(user, targetStatus);
        return UseCaseResult<KycReviewResultDto>.Success(new KycReviewResultDto(
            user.UserId,
            targetStatus,
            effectiveAccountStatus,
            resultMessage,
            now,
            officer.FullName,
            reason));
    }

    private static string GetCurrentVerificationStatus(User user)
    {
        if (user.FarmerProfile is not null)
        {
            return user.FarmerProfile.VerificationStatus;
        }

        if (user.WholesaleBuyerProfile is not null)
        {
            return user.WholesaleBuyerProfile.VerificationStatus;
        }

        return VerificationStatuses.PendingApproval;
    }

    private static string ComputeEffectiveAccountStatus(User user, string verificationStatus)
    {
        if (user.Status == "Suspended")
        {
            return "Suspended";
        }

        return verificationStatus switch
        {
            VerificationStatuses.Approved => "Active",
            VerificationStatuses.Rejected => "Rejected",
            _ => "Pending"
        };
    }

    private static UseCaseResult<T> Denied<T>() => UseCaseResult<T>.Failure(
        "FORBIDDEN",
        "Bạn không có quyền truy cập chức năng thẩm định KYC.",
        "Forbidden");

    private static UseCaseResult<T> Missing<T>() => UseCaseResult<T>.Failure(
        "NOT_FOUND",
        "Không tìm thấy hồ sơ KYC.",
        "NotFound");
}

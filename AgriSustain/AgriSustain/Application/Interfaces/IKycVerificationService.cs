using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Verification;

namespace AgriSustain.Application.Interfaces;

public interface IKycVerificationService
{
    Task<UseCaseResult<Page<KycApplicationSummaryDto>>> SearchAsync(
        KycApplicationSearch search,
        CancellationToken cancellationToken);

    Task<UseCaseResult<KycApplicationDetailDto>> GetDetailAsync(
        long userId,
        CancellationToken cancellationToken);

    Task<UseCaseResult<KycReviewResultDto>> ReviewAsync(
        long userId,
        KycReviewDecisionRequest request,
        CancellationToken cancellationToken);
}


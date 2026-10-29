using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Promotions;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-PS-01 Manage Promotions and Vouchers for Promotion Staff.</summary>
public interface IVoucherManagementService
{
    Task<Result<VoucherListDto>> SearchAsync(
        VoucherQuery query,
        CancellationToken cancellationToken);

    Task<Result<VoucherDto>> GetAsync(
        long voucherId,
        CancellationToken cancellationToken);

    Task<Result<VoucherActionResponse>> CreateAsync(
        SaveVoucherRequest request,
        CancellationToken cancellationToken);

    Task<Result<VoucherActionResponse>> UpdateAsync(
        long voucherId,
        SaveVoucherRequest request,
        CancellationToken cancellationToken);

    Task<Result<VoucherActionResponse>> SetActiveAsync(
        long voucherId,
        bool isActive,
        CancellationToken cancellationToken);

    Task<Result<VoucherActionResponse>> DeleteAsync(
        long voucherId,
        CancellationToken cancellationToken);
}

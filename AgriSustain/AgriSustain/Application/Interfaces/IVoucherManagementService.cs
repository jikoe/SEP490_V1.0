using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Promotions;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-PS-01 Manage Promotions and Vouchers for Promotion Staff.</summary>
public interface IVoucherManagementService
{
    Task<Result<VoucherListDto>> SearchAsync(
        VoucherQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<VoucherDto>> GetAsync(
        long voucherId,
        CancellationToken cancellationToken = default);

    Task<Result<VoucherActionResponse>> CreateAsync(
        SaveVoucherRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<VoucherActionResponse>> UpdateAsync(
        long voucherId,
        SaveVoucherRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<VoucherActionResponse>> SetActiveAsync(
        long voucherId,
        bool isActive,
        string? version,
        CancellationToken cancellationToken = default);

    Task<Result<VoucherActionResponse>> DeleteAsync(
        long voucherId,
        string? version,
        CancellationToken cancellationToken = default);
}

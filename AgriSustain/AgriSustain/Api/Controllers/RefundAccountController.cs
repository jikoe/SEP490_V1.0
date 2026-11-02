using AgriSustain.Application.Services.Refunds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-FIN-08 Provide Refund Account. Users only see and change their own refund requests.</summary>
[ApiController]
[Authorize(Roles = "RetailCustomer,WholesaleBuyer")]
[Route("api/profile/refunds")]
public sealed class RefundAccountController(IRefundAccountService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        ApiResults.From(await service.ListMineAsync(cancellationToken), HttpContext);

    [HttpGet("{refundId:long}")]
    public async Task<IActionResult> Get(long refundId, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetAsync(refundId, cancellationToken), HttpContext);

    /// <summary>OTP needed only when "Also save as my default bank account" is ticked (BR-BA-04).</summary>
    [HttpPost("otp")]
    public async Task<IActionResult> RequestOtp(CancellationToken cancellationToken)
    {
        var result = await service.RequestOtpAsync(cancellationToken);
        return result.IsSuccess ? Ok(new { message = result.Value }) : ApiResults.From(result, HttpContext);
    }

    [HttpPut("{refundId:long}/account")]
    public async Task<IActionResult> ProvideAccount(long refundId, [FromBody] ProvideRefundAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.ProvideAccountAsync(refundId, request, cancellationToken);
        if (!result.IsSuccess) return ApiResults.From(result, HttpContext);

        // MSG-RF-05
        string amount = result.Value!.Amount.ToString("N0", new System.Globalization.CultureInfo("vi-VN"));
        return Ok(new
        {
            message = $"Đã lưu tài khoản nhận hoàn tiền. AgriSustain sẽ chuyển {amount} ₫ vào {result.Value.Receiver?.BankShortName} {result.Value.Receiver?.MaskedAccountNumber}.",
            refund = result.Value
        });
    }
}

using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>
/// UC-AUTH-03 Manage Profile - Bank Account tab. A user only manages their own account; there is
/// no endpoint for staff to read or edit another user's bank account (BR-BA-04).
/// </summary>
[ApiController]
[Authorize(Roles = "Farmer,RetailCustomer,WholesaleBuyer")]
[Route("api/profile/bank-account")]
public sealed class BankAccountController(IBankAccountService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetAsync(cancellationToken), HttpContext);

    /// <summary>Step 3: look up the holder name (called about 600 ms after the user stops typing).</summary>
    [HttpPost("lookup")]
    public async Task<IActionResult> Lookup(
        [FromBody] BankLookupRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.LookupAsync(request, cancellationToken), HttpContext);

    /// <summary>Step 5: send the 6-digit OTP to the account e-mail (also used to resend after 60 s).</summary>
    [HttpPost("otp")]
    public async Task<IActionResult> RequestOtp(CancellationToken cancellationToken) =>
        ApiResults.From(await service.RequestOtpAsync(cancellationToken), HttpContext);

    [HttpPut]
    public async Task<IActionResult> Save(
        [FromBody] SaveBankAccountRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.SaveAsync(request, cancellationToken), HttpContext);

    [HttpPost("remove")]
    public async Task<IActionResult> Remove(
        [FromBody] RemoveBankAccountRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.RemoveAsync(request, cancellationToken), HttpContext);
}

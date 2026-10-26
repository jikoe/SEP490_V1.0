using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuthenticationService _registrationService;

    public AuthController(IAuthService authService, IAuthenticationService registrationService)
    {
        _authService = authService;
        _registrationService = registrationService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.LoginAsync(request, cancellationToken);
        if (result == null)
        {
            return Problem(
                title: "Đăng nhập thất bại",
                detail: "Tên đăng nhập hoặc mật khẩu không đúng.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await _registrationService.InitiateRegistrationAsync(request, cancellationToken), HttpContext);

    [AllowAnonymous]
    [HttpPost("verify-otp")]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await _registrationService.VerifyOtpAndCompleteRegistrationAsync(request, cancellationToken), HttpContext);

    [AllowAnonymous]
    [HttpPost("resend-otp")]
    public async Task<IActionResult> ResendOtp(
        [FromBody] ResendOtpRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await _registrationService.ResendOtpAsync(request, cancellationToken), HttpContext);

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(
            await _authService.ChangePasswordAsync(request, cancellationToken),
            HttpContext);
}

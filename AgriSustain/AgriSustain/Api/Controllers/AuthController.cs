using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuthenticationService _registrationService;
    private readonly RevokedTokenStore _revokedTokenStore;

    public AuthController(
        IAuthService authService,
        IAuthenticationService registrationService,
        RevokedTokenStore revokedTokenStore)
    {
        _authService = authService;
        _registrationService = registrationService;
        _revokedTokenStore = revokedTokenStore;
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
        return result.Status switch
        {
            LoginResultStatus.Success when result.Response is not null => Ok(result.Response),
            LoginResultStatus.InvalidCredentials => Problem(
                title: "Đăng nhập thất bại",
                detail: "Tên đăng nhập hoặc mật khẩu không đúng.",
                statusCode: StatusCodes.Status401Unauthorized),
            LoginResultStatus.AccountLocked => Problem(
                title: "Tài khoản đang bị khóa",
                detail: "Tài khoản đang bị khóa tạm thời do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau.",
                statusCode: StatusCodes.Status423Locked),
            LoginResultStatus.AccountSuspended => Problem(
                title: "Tài khoản đã bị tạm ngưng",
                detail: "Tài khoản đã bị Admin tạm ngưng. Vui lòng liên hệ bộ phận hỗ trợ để biết thêm thông tin.",
                statusCode: StatusCodes.Status403Forbidden),
            _ => throw new InvalidOperationException("Kết quả đăng nhập không hợp lệ.")
        };
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var tokenId = User.FindFirstValue(JwtRegisteredClaimNames.Jti)
            ?? User.FindFirstValue(ClaimTypes.SerialNumber);
        var expiration = User.FindFirstValue(JwtRegisteredClaimNames.Exp)
            ?? User.FindFirstValue(ClaimTypes.Expiration);
        if (string.IsNullOrWhiteSpace(tokenId)
            || !long.TryParse(expiration, out var expirationTimestamp))
        {
            return Problem(
                title: "Phiên đăng nhập không hợp lệ",
                detail: "Không thể xác thực phiên đăng nhập hiện tại.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        _revokedTokenStore.Revoke(
            tokenId,
            DateTimeOffset.FromUnixTimeSeconds(expirationTimestamp));
        return NoContent();
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

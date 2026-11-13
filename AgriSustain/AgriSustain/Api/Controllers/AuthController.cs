using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgriSustain.Application.Common;
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
    private readonly IPasswordResetService _passwordResetService;
    private readonly RevokedTokenStore _revokedTokenStore;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly string? _supportEmail;

    public AuthController(
        IAuthService authService,
        IAuthenticationService registrationService,
        IPasswordResetService passwordResetService,
        RevokedTokenStore revokedTokenStore,
        IRefreshTokenService refreshTokenService,
        AccountNotificationSettings? notificationSettings = null)
    {
        _supportEmail = notificationSettings?.SupportEmail;
        _authService = authService;
        _registrationService = registrationService;
        _passwordResetService = passwordResetService;
        _revokedTokenStore = revokedTokenStore;
        _refreshTokenService = refreshTokenService;
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
                detail: SuspendedMessage(_supportEmail),
                statusCode: StatusCodes.Status403Forbidden),
            _ => throw new InvalidOperationException("Kết quả đăng nhập không hợp lệ.")
        };
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            return Problem(
                title: "Yêu cầu không hợp lệ",
                detail: "RefreshToken không được để trống.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await _refreshTokenService.RotateRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (!result.IsSuccess)
        {
            return Problem(
                title: "Làm mới phiên thất bại",
                detail: result.Error?.Message ?? "Không thể làm mới phiên đăng nhập.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(result.Value);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequestDto? request, CancellationToken cancellationToken)
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

        await _revokedTokenStore.RevokeAsync(
            tokenId,
            DateTimeOffset.FromUnixTimeSeconds(expirationTimestamp),
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            await _refreshTokenService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
        }
        else if (long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await _refreshTokenService.RevokeAllForUserAsync(userId, cancellationToken);
        }

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

    /// <summary>
    /// POST api/auth/forgot-password — UC-AUTH-07 step 1, anonymous. Always answers with the same neutral message.
    /// </summary>
    /// <param name="request">E-mail address.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with MSG-OTP-04; 400 for an invalid e-mail.</returns>
    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await _passwordResetService.RequestCodeAsync(request, cancellationToken), HttpContext);

    /// <summary>
    /// POST api/auth/forgot-password/resend-otp — resend the reset code, anonymous.
    /// </summary>
    /// <param name="request">E-mail address.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the neutral message; 400 for an invalid e-mail.</returns>
    [AllowAnonymous]
    [HttpPost("forgot-password/resend-otp")]
    public async Task<IActionResult> ResendPasswordResetOtp(
        [FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await _passwordResetService.ResendCodeAsync(request, cancellationToken), HttpContext);

    /// <summary>
    /// POST api/auth/forgot-password/verify-otp — UC-AUTH-07 step 2: exchange the code for a 10-minute reset token.
    /// </summary>
    /// <param name="request">E-mail and code.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200 with the reset token; 400 for a wrong, expired, used or locked code (INVALID_OTP, OTP_EXPIRED,
    /// OTP_ALREADY_USED, MAX_ATTEMPTS_EXCEEDED).</returns>
    [AllowAnonymous]
    [HttpPost("forgot-password/verify-otp")]
    public async Task<IActionResult> VerifyPasswordResetOtp(
        [FromBody] VerifyOtpRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await _passwordResetService.VerifyCodeAsync(request, cancellationToken), HttpContext);

    /// <summary>
    /// POST api/auth/reset-password — UC-AUTH-07 step 3: set the new password with the reset token.
    /// </summary>
    /// <param name="request">E-mail, reset token, new password and confirmation.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>200; 400 for invalid or reused passwords; 401 for an invalid reset session.</returns>
    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await _passwordResetService.ResetPasswordAsync(request, cancellationToken), HttpContext);

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ChangePasswordAsync(request, cancellationToken);
        return result.IsSuccess ? new JsonResult(result.Value) : ApiResults.From(result, HttpContext);
    }

    /// <summary>MSG-UM-11: a suspended account is told the support address to contact.</summary>
    public static string SuspendedMessage(string? supportEmail) => string.IsNullOrWhiteSpace(supportEmail)
        ? "Tài khoản của bạn đã bị tạm ngưng. Vui lòng liên hệ bộ phận hỗ trợ."
        : $"Tài khoản của bạn đã bị tạm ngưng. Vui lòng liên hệ bộ phận hỗ trợ qua {supportEmail.Trim()}.";
}

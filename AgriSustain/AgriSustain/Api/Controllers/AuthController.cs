using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return result is null
            ? Problem(
                title: "Đăng nhập thất bại",
                detail: "Tên đăng nhập hoặc mật khẩu không đúng.",
                statusCode: StatusCodes.Status401Unauthorized)
            : Ok(result);
    }
}

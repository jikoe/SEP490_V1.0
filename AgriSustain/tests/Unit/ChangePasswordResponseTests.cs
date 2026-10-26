using AgriSustain.Api.Controllers;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class ChangePasswordResponseTests
{
    private const string SuccessMessage =
        "Đổi mật khẩu thành công. Vui lòng sử dụng mật khẩu mới cho những lần đăng nhập sau.";

    [Fact]
    public async Task Success_is_JSON_so_the_browser_can_render_the_confirmation()
    {
        var controller = Controller(Result<string>.SuccessResult(SuccessMessage));

        var response = await controller.ChangePassword(new("Old12345", "New12345", "New12345"), default);

        var json = Assert.IsType<JsonResult>(response);
        Assert.Equal(SuccessMessage, json.Value);
    }

    [Fact]
    public async Task Incorrect_current_password_keeps_the_existing_problem_contract()
    {
        var controller = Controller(Result<string>.FailureResult(new Error(
            "INVALID_CURRENT_PASSWORD", "Mật khẩu hiện tại không chính xác.")));

        var response = await controller.ChangePassword(new("Old12345", "New12345", "New12345"), default);

        var problemResponse = Assert.IsType<ObjectResult>(response);
        Assert.Equal(400, problemResponse.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResponse.Value);
        Assert.Equal("INVALID_CURRENT_PASSWORD", problem.Extensions["code"]);
    }

    private static AuthController Controller(Result<string> result) => new(new Auth(result), null!)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private sealed class Auth(Result<string> result) : IAuthService
    {
        public Task<LoginResponseDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<string>> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}

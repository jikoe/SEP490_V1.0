using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Infrastructure.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Application.Services;

public sealed class AuthService : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private readonly IPasswordService _passwordService;
    private readonly ITokenIssuer _tokenIssuer;
    private readonly TimeProvider _timeProvider;
    private readonly IUserRepository _userRepository;
    private readonly AgriSustainContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;

    public AuthService(
        IUserRepository userRepository,
        IPasswordService passwordService,
        ITokenIssuer tokenIssuer,
        TimeProvider timeProvider,
        AgriSustainContext context,
        ICurrentUser currentUser,
        IValidator<ChangePasswordRequest> changePasswordValidator)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
        _tokenIssuer = tokenIssuer;
        _timeProvider = timeProvider;
        _context = context;
        _currentUser = currentUser;
        _changePasswordValidator = changePasswordValidator;
    }

    public async Task<LoginResponseDto?> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var user = await _userRepository.FindByUsernameOrEmailAsync(
            identifier,
            cancellationToken);

        if (user is null || user.Status != "Active")
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > now.UtcDateTime)
        {
            return null;
        }

        if (user.LockedUntil.HasValue)
        {
            user.LockedUntil = null;
            user.FailedLoginCount = 0;
        }

        var verification = _passwordService.Verify(user, request.Password);
        if (!verification.IsValid)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLoginAttempts)
            {
                user.LockedUntil = now.Add(LockoutDuration).UtcDateTime;
            }

            await _userRepository.SaveChangesAsync(cancellationToken);
            return null;
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        if (verification.RequiresRehash)
        {
            user.PasswordHash = _passwordService.Hash(user, request.Password);
        }

        await _userRepository.SaveChangesAsync(cancellationToken);

        var token = _tokenIssuer.Issue(user, now);
        return new LoginResponseDto(
            token.AccessToken,
            token.ExpiresAt.UtcDateTime,
            new AuthenticatedUserDto(
                user.UserId,
                user.Username,
                user.Email,
                user.FullName,
                user.Role));
    }

    // Retain the existing password-change endpoint while importing TuTB's login services.
    public async Task<Result<string>> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var error = await RequestValidation.CheckAsync(
            _changePasswordValidator, request, cancellationToken);
        if (error != null)
            return Result<string>.FailureResult(error);

        var userId = _currentUser.UserId;
        var user = userId is null ? null : await _context.Users.SingleOrDefaultAsync(
            candidate => candidate.UserId == userId, cancellationToken);
        if (user is null || user.Status != "Active")
            return Result<string>.FailureResult(new Error(
                "UNAUTHORIZED", "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.",
                "Unauthorized"));

        if (!_passwordService.Verify(user, request.CurrentPassword).IsValid)
            return Result<string>.FailureResult(new Error(
                "INVALID_CURRENT_PASSWORD", "Mật khẩu hiện tại không chính xác."));

        user.PasswordHash = _passwordService.Hash(user, request.NewPassword);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<string>.SuccessResult(
            "Đổi mật khẩu thành công. Vui lòng sử dụng mật khẩu mới cho những lần đăng nhập sau.");
    }
}

using AgentMemoryService.Shared.Core;
using AgentMemoryService.Users.Application.Commands;
using AgentMemoryService.Users.Core.Entities;
using AgentMemoryService.Users.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AgentMemoryService.Shared.Infrastructure;

namespace AgentMemoryService.Users.Infrastructure.Handlers;

// ─────────────────────────────────────────────────────────────
// Login Handler
// ─────────────────────────────────────────────────────────────
public sealed class LoginHandler : IRequestHandler<LoginCommand, Result<AuthTokenResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public LoginHandler(UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<Result<AuthTokenResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user is null || !user.IsActive)
            return Result<AuthTokenResponse>.Failure(
                Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password"));

        var valid = await _userManager.CheckPasswordAsync(user, command.Password);
        if (!valid)
            return Result<AuthTokenResponse>.Failure(
                Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password"));

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        return Result<AuthTokenResponse>.Success(
            new AuthTokenResponse(accessToken, refreshToken, user.RefreshTokenExpiry.Value));
    }
}

// ─────────────────────────────────────────────────────────────
// Refresh Token Handler
// ─────────────────────────────────────────────────────────────
public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, Result<AuthTokenResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public RefreshTokenHandler(UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<Result<AuthTokenResponse>> Handle(RefreshTokenCommand command, CancellationToken ct)
    {
        var user = await _userManager.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == command.RefreshToken, ct);

        if (user is null || !user.IsActive)
            return Result<AuthTokenResponse>.Failure(
                Error.Unauthorized("Auth.InvalidRefreshToken", "Invalid or expired refresh token"));

        if (user.RefreshTokenExpiry is null || user.RefreshTokenExpiry < DateTime.UtcNow)
            return Result<AuthTokenResponse>.Failure(
                Error.Unauthorized("Auth.RefreshTokenExpired", "Refresh token has expired, please log in again"));

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        return Result<AuthTokenResponse>.Success(
            new AuthTokenResponse(accessToken, refreshToken, user.RefreshTokenExpiry.Value));
    }
}

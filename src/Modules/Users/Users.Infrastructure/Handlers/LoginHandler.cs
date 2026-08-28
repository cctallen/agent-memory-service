using AgentMemoryService.Shared.Core;
using AgentMemoryService.Users.Application.Commands;
using AgentMemoryService.Users.Core.Entities;
using AgentMemoryService.Users.Infrastructure.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace AgentMemoryService.Users.Infrastructure.Handlers;

public sealed class LoginHandler : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public LoginHandler(UserManager<ApplicationUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<Result<LoginResponse>> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await _userManager.FindByEmailAsync(command.Email);
        if (user is null || !user.IsActive)
            return Result<LoginResponse>.Failure(Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password"));

        var valid = await _userManager.CheckPasswordAsync(user, command.Password);
        if (!valid)
            return Result<LoginResponse>.Failure(Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password"));

        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshToken = _tokenService.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        return Result<LoginResponse>.Success(new LoginResponse(accessToken, refreshToken, user.RefreshTokenExpiry.Value));
    }
}

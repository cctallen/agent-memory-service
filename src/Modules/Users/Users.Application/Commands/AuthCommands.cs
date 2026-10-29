using AgentMemoryService.Shared.Core;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using AgentMemoryService.Users.Core.Entities;

namespace AgentMemoryService.Users.Application.Commands;

// ─────────────────────────────────────────────────────────────
// Shared response types
// ─────────────────────────────────────────────────────────────
public sealed record AuthTokenResponse(string AccessToken, string RefreshToken, DateTime ExpiresAt);

// ─────────────────────────────────────────────────────────────
// Register
// ─────────────────────────────────────────────────────────────
public sealed record RegisterCommand(string Email, string Password, string FirstName, string LastName)
    : IRequest<Result<RegisterResponse>>;

public sealed record RegisterResponse(Guid UserId, string Email, string FullName);

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MaximumLength(255);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain a number");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100);
    }
}

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, Result<RegisterResponse>>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public RegisterHandler(UserManager<ApplicationUser> userManager) => _userManager = userManager;

    public async Task<Result<RegisterResponse>> Handle(RegisterCommand command, CancellationToken ct)
    {
        var existing = await _userManager.FindByEmailAsync(command.Email);
        if (existing is not null)
            return Result<RegisterResponse>.Failure(
                Error.Conflict("User.DuplicateEmail", "Email already registered"));

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            UserName = command.Email,
            FirstName = command.FirstName,
            LastName = command.LastName
        };

        var identityResult = await _userManager.CreateAsync(user, command.Password);
        if (!identityResult.Succeeded)
        {
            var errors = string.Join("; ", identityResult.Errors.Select(e => e.Description));
            return Result<RegisterResponse>.Failure(
                Error.Validation("User.CreateFailed", errors));
        }

        return Result<RegisterResponse>.Success(
            new RegisterResponse(user.Id, user.Email!, user.FullName));
    }
}

// ─────────────────────────────────────────────────────────────
// Login
// ─────────────────────────────────────────────────────────────
public sealed record LoginCommand(string Email, string Password)
    : IRequest<Result<AuthTokenResponse>>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}

// ─────────────────────────────────────────────────────────────
// Refresh Token
// ─────────────────────────────────────────────────────────────
public sealed record RefreshTokenCommand(string RefreshToken)
    : IRequest<Result<AuthTokenResponse>>;

public sealed class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required");
    }
}

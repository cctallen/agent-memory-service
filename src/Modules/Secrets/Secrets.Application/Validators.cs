using FluentValidation;
using AgentMemoryService.Secrets.Application.Commands;

namespace AgentMemoryService.Secrets.Application;

public sealed class CreateSecretValidator : AbstractValidator<CreateSecretCommand>
{
    public CreateSecretValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200)
            .Matches(@"^[a-zA-Z0-9_\-\.]+$").WithMessage("Name may only contain letters, numbers, underscores, hyphens, and dots");

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Value is required")
            .MaximumLength(10000);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}

public sealed class UpdateSecretValidator : AbstractValidator<UpdateSecretCommand>
{
    public UpdateSecretValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200)
            .Matches(@"^[a-zA-Z0-9_\-\.]+$").WithMessage("Name may only contain letters, numbers, underscores, hyphens, and dots");

        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Value is required")
            .MaximumLength(10000);

        RuleFor(x => x.Description)
            .MaximumLength(500);
    }
}

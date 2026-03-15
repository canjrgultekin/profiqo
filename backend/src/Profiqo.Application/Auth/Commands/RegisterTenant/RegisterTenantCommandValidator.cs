using FluentValidation;

namespace Profiqo.Application.Auth.Commands.RegisterTenant;

internal sealed class RegisterTenantCommandValidator : AbstractValidator<RegisterTenantCommand>
{
    public RegisterTenantCommandValidator()
    {
        RuleFor(x => x.TenantName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.TenantSlug)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(80)
            .Matches("^[a-z0-9]+(?:-[a-z0-9]+)*$")
            .WithMessage("TenantSlug must be lowercase alphanumeric and may include hyphens.");

        RuleFor(x => x.OwnerEmail)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();

        RuleFor(x => x.OwnerPassword)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(200);

        RuleFor(x => x.OwnerDisplayName)
            .NotEmpty()
            .MaximumLength(200);
    }
}
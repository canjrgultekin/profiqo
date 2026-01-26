using FluentValidation;

namespace Profiqo.Application.Auth.Commands.Login;

internal sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.TenantSlug)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(80);

        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(254)
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(1)
            .MaximumLength(200);
    }
}
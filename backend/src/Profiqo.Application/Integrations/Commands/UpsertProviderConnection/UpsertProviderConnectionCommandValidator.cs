using FluentValidation;

namespace Profiqo.Application.Integrations.Commands.UpsertProviderConnection;

internal sealed class UpsertProviderConnectionCommandValidator : AbstractValidator<UpsertProviderConnectionCommand>
{
    public UpsertProviderConnectionCommandValidator()
    {
        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ExternalAccountId)
            .MaximumLength(200);

        RuleFor(x => x.AccessToken)
            .NotEmpty()
            .MinimumLength(10)
            .MaximumLength(4000);

        RuleFor(x => x.RefreshToken)
            .MaximumLength(4000);
    }
}
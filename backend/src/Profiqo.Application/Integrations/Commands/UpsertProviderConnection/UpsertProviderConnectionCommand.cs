using Profiqo.Application.Common.Messaging;
using Profiqo.Domain.Integrations;

namespace Profiqo.Application.Integrations.Commands.UpsertProviderConnection;

public sealed record UpsertProviderConnectionCommand(
    ProviderType ProviderType,
    string DisplayName,
    string? ExternalAccountId,
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? AccessTokenExpiresAtUtc
) : ICommand<Guid>;
using Profiqo.Application.Auth.DTOs;
using Profiqo.Application.Common.Messaging;

namespace Profiqo.Application.Auth.Commands.Login;

public sealed record LoginCommand(
    string TenantSlug,
    string Email,
    string Password
) : ICommand<LoginResultDto>;
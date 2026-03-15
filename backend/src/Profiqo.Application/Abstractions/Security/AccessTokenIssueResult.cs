namespace Profiqo.Application.Abstractions.Security;

public sealed record AccessTokenIssueResult(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc);
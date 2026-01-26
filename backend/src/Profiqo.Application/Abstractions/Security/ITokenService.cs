using Profiqo.Domain.Users;

namespace Profiqo.Application.Abstractions.Security;

public interface ITokenService
{
    AccessTokenIssueResult IssueAccessToken(User user, DateTimeOffset nowUtc);
}
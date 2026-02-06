namespace Profiqo.Application.Common.Exceptions;

public sealed class ExternalServiceAuthException : AppException
{
    public string Provider { get; }

    public ExternalServiceAuthException(string provider, string message)
        : base("integration_auth_failed", message)
    {
        Provider = provider;
    }
}
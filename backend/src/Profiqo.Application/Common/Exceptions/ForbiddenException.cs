namespace Profiqo.Application.Common.Exceptions;

public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string message)
        : base("forbidden", message)
    {
    }
}
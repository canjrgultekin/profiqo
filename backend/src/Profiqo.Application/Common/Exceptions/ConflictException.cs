namespace Profiqo.Application.Common.Exceptions;

public sealed class ConflictException : AppException
{
    public ConflictException(string message)
        : base("conflict", message)
    {
    }
}
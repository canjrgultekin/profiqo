namespace Profiqo.Application.Common.Exceptions;

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message)
        : base("not_found", message)
    {
    }
}
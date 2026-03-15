namespace Profiqo.Application.Common.Exceptions;

public abstract class AppException : Exception
{
    public string Code { get; }

    protected AppException(string code, string message) : base(message)
    {
        Code = code;
    }
}
namespace Profiqo.Application.Common.Exceptions;

public sealed class AppValidationException : AppException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public AppValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("validation_error", "Validation failed.")
    {
        Errors = errors;
    }
}
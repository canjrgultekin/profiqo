namespace Profiqo.Domain.Common;

public static class Guard
{
    public static string AgainstNullOrWhiteSpace(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{name} cannot be null or whitespace.");

        return value.Trim();
    }

    public static string AgainstTooLong(string value, int maxLen, string name)
    {
        if (value.Length > maxLen)
            throw new DomainException($"{name} cannot be longer than {maxLen} characters.");

        return value;
    }

    public static Guid AgainstEmpty(Guid value, string name)
    {
        if (value == Guid.Empty)
            throw new DomainException($"{name} cannot be empty.");

        return value;
    }

    public static int AgainstOutOfRange(int value, int minInclusive, int maxInclusive, string name)
    {
        if (value < minInclusive || value > maxInclusive)
            throw new DomainException($"{name} must be between {minInclusive} and {maxInclusive}.");

        return value;
    }

    public static decimal AgainstOutOfRange(decimal value, decimal minInclusive, decimal maxInclusive, string name)
    {
        if (value < minInclusive || value > maxInclusive)
            throw new DomainException($"{name} must be between {minInclusive} and {maxInclusive}.");

        return value;
    }

    public static DateTimeOffset EnsureUtc(DateTimeOffset value, string name)
    {
        _ = name;
        return value.ToUniversalTime();
    }

    public static IReadOnlyCollection<T> AgainstNullOrEmpty<T>(IEnumerable<T>? value, string name)
    {
        if (value is null)
            throw new DomainException($"{name} cannot be null.");

        var list = value.ToList();
        if (list.Count == 0)
            throw new DomainException($"{name} cannot be empty.");

        return new ReadOnlyCollection<T>(list);
    }
}
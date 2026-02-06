namespace Profiqo.Application.Abstractions.Time;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
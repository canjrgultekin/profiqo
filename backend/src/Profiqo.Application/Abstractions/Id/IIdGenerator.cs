namespace Profiqo.Application.Abstractions.Id;

public interface IIdGenerator
{
    Guid NewGuid();
}
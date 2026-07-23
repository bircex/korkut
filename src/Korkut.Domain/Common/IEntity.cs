namespace Korkut.Domain.Common;

/// <summary>An entity with a surrogate UUIDv7 identity. Enables generic, key-based repository operations.</summary>
public interface IEntity
{
    Guid Id { get; }
}

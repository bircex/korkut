using Korkut.Domain.Ids;

namespace Korkut.Domain.Common;

/// <summary>
/// Base for domain entities keyed by a UUIDv7 identity. Equality is by id, so two loads of the
/// same row compare equal. Domain owns these primitives and depends on nothing else.
/// </summary>
public abstract class Entity : IEntity, IEquatable<Entity>
{
    protected Entity(Guid id) => Id = id;

    /// <summary>Materialization constructor for the ORM; domain code uses the id-taking one.</summary>
    protected Entity() { }

    public Guid Id { get; private set; }

    /// <summary>Factory helper for new entities using the platform id strategy.</summary>
    protected static Guid NewId() => UuidV7.NewId();

    public bool Equals(Entity? other) =>
        other is not null && other.GetType() == GetType() && other.Id == Id;

    public override bool Equals(object? obj) => obj is Entity e && Equals(e);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

using Korkut.SharedKernel.Ids;

namespace Korkut.Domain.Primitives;

/// <summary>Marks the root of a consistency/transaction boundary.</summary>
public interface IAggregateRoot;

/// <summary>
/// Base for domain entities keyed by a UUIDv7 identity. Equality is by id, so two
/// loads of the same row compare equal. Domain code depends only on SharedKernel.
/// </summary>
public abstract class Entity : IEquatable<Entity>
{
    protected Entity(Guid id) => Id = id;

    public Guid Id { get; }

    /// <summary>Factory helper for new entities using the platform id strategy.</summary>
    protected static Guid NewId() => UuidV7.NewId();

    public bool Equals(Entity? other) =>
        other is not null && other.GetType() == GetType() && other.Id == Id;

    public override bool Equals(object? obj) => obj is Entity e && Equals(e);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}

namespace Korkut.Domain.Common;

/// <summary>An entity that is retired by stamping a deletion time rather than removing the row.</summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; }
}

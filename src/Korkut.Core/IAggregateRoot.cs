namespace Korkut.Core;

/// <summary>
/// Marks the root of a consistency/transaction boundary. Repositories are defined for
/// aggregate roots only; everything else is reached through its root.
/// </summary>
public interface IAggregateRoot;

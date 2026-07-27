using Korkut.Core;

namespace Korkut.Matters;

/// <summary>Persistence port for matters. Reads exclude soft-deleted rows, newest first.</summary>
public interface IMatterRepository : ISoftDeleteRepository<Matter>;

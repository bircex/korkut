using Korkut.Application.Common;
using Korkut.Domain.Matters;

namespace Korkut.Application.Matters;

/// <summary>Persistence port for matters. Reads exclude soft-deleted rows, newest first.</summary>
public interface IMatterRepository : ISoftDeleteRepository<Matter>;

using Korkut.Matters.Entities;
using Korkut.Core;

namespace Korkut.Matters.Repositories;

/// <summary>Persistence port for matters. Reads exclude soft-deleted rows, newest first.</summary>
internal interface IMatterRepository : ISoftDeleteRepository<Matter>;

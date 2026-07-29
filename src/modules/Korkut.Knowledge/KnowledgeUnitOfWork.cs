using Korkut.Persistence;
using Korkut.Core;
using Korkut.Knowledge.Persistence;

namespace Korkut.Knowledge;

/// <summary>
/// <see cref="IUnitOfWork"/> for the Knowledge host: the same tenant-pinned transaction as the
/// operational one, but over the Knowledge DB — so RLS scopes the vector store the ingestion and
/// retrieval services reach through <see cref="Korkut.Knowledge.IVectorStoreService"/>.
/// The scope spans the embedder call too, mirroring how the Agent holds its scope across the LLM.
/// </summary>
public sealed class KnowledgeUnitOfWork(KnowledgeDbContext db) : IUnitOfWork
{
    public async Task<T> InTenantScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default)
    {
        await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
        var result = await work();
        await scope.CommitAsync(ct);
        return result;
    }
}

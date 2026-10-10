using Korkut.Persistence;
using Korkut.Core;
using Korkut.Knowledge.Persistence;

namespace Korkut.Knowledge;

/// <summary>
/// The unit of work over the Knowledge DB. Its own type, not <see cref="IUnitOfWork"/>: the host
/// also runs the operational modules, whose <see cref="IUnitOfWork"/> pins the operational DB, and
/// one registration of a shared interface would silently send the other side's transactions to
/// the wrong database.
/// </summary>
public interface IKnowledgeUnitOfWork : IUnitOfWork;

/// <summary>
/// <see cref="IKnowledgeUnitOfWork"/>: the same tenant-pinned transaction as the operational one,
/// but over the Knowledge DB, so RLS scopes the vector store the ingestion and retrieval services
/// reach through <see cref="Korkut.Knowledge.IVectorStoreService"/>. The scope spans the embedder
/// call too, mirroring how the Agent holds its scope across the LLM.
/// </summary>
public sealed class KnowledgeUnitOfWork(KnowledgeDbContext db) : IKnowledgeUnitOfWork
{
    public async Task<T> InTenantScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default)
    {
        await using var scope = await db.BeginTenantScopeAsync(officeId, ct);
        var result = await work();
        await scope.CommitAsync(ct);
        return result;
    }
}

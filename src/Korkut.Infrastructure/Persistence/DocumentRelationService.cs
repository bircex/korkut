using Korkut.Application.Auditing;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Ids;
using Korkut.Domain.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>The kinds of tenant entity a document may be linked to (KDM-028).</summary>
public static class DocumentRelationTargets
{
    public const string Client = "client";
    public const string Matter = "matter";
    public const string Party = "party";

    public static bool IsValid(string type) => type is Client or Matter or Party;
}

/// <summary>
/// Manages typed document ⇄ (client|matter|party) relations (KDM-028). Every link and
/// unlink is:
/// <list type="bullet">
/// <item><b>authorized</b> — the document and the target must both exist in the caller's
/// office (RLS + explicit existence check), so a relation can never span tenants;</item>
/// <item><b>audited</b> — an append-only audit entry records the change with before/after;</item>
/// <item><b>idempotent</b> — the unique link index turns a repeat link into an update.</item>
/// </list>
/// At most one relation per document is the primary context.
/// </summary>
public sealed class DocumentRelationService(KorkutDbContext db, IAuditLog audit, IClock clock)
{
    /// <summary>
    /// Links a document to a target. Fails if either side is missing in this office.
    /// Re-linking the same pair updates the primary flag rather than duplicating.
    /// </summary>
    public async Task<Guid> LinkAsync(
        Guid officeId, Guid actorId, Guid documentId, string targetType, Guid targetId, bool isPrimary, CancellationToken ct = default)
    {
        if (!DocumentRelationTargets.IsValid(targetType))
        {
            throw new ArgumentException($"Unknown target type '{targetType}'.", nameof(targetType));
        }

        // Authorization: both the document and the target must belong to this office.
        await EnsureDocumentAsync(officeId, documentId, ct);
        await EnsureTargetAsync(officeId, targetType, targetId, ct);

        if (isPrimary)
        {
            await ClearPrimaryAsync(officeId, documentId, ct);
        }

        var existing = await db.DocumentRelations.SingleOrDefaultAsync(
            x => x.OfficeId == officeId && x.DocumentId == documentId && x.TargetType == targetType && x.TargetId == targetId,
            ct);

        if (existing is not null)
        {
            var before = new { existing.IsPrimary };
            existing.IsPrimary = isPrimary;
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(new AuditEntry(
                officeId, actorId, null, null, "document.relation.updated", "document_relation", existing.Id,
                before, new { existing.IsPrimary }), ct);
            return existing.Id;
        }

        var relation = new DocumentRelationEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            DocumentId = documentId,
            TargetType = targetType,
            TargetId = targetId,
            IsPrimary = isPrimary,
            CreatedBy = actorId,
            CreatedAt = clock.UtcNow,
        };
        db.DocumentRelations.Add(relation);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditEntry(
            officeId, actorId, null, null, "document.relation.linked", "document_relation", relation.Id,
            null, new { relation.DocumentId, relation.TargetType, relation.TargetId, relation.IsPrimary }), ct);
        return relation.Id;
    }

    /// <summary>Removes a link and records the removal in the audit trail.</summary>
    public async Task UnlinkAsync(Guid officeId, Guid actorId, Guid documentId, string targetType, Guid targetId, CancellationToken ct = default)
    {
        var relation = await db.DocumentRelations.SingleOrDefaultAsync(
            x => x.OfficeId == officeId && x.DocumentId == documentId && x.TargetType == targetType && x.TargetId == targetId,
            ct) ?? throw new InvalidOperationException("Relation not found.");

        db.DocumentRelations.Remove(relation);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditEntry(
            officeId, actorId, null, null, "document.relation.unlinked", "document_relation", relation.Id,
            new { relation.DocumentId, relation.TargetType, relation.TargetId, relation.IsPrimary }, null), ct);
    }

    /// <summary>
    /// Returns the ids of documents visible through a given target (permission
    /// inheritance: whoever may access the target may see its linked documents).
    /// </summary>
    public async Task<IReadOnlyList<Guid>> DocumentsForTargetAsync(Guid officeId, string targetType, Guid targetId, CancellationToken ct = default)
        => await db.DocumentRelations.AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.TargetType == targetType && x.TargetId == targetId)
            .Select(x => x.DocumentId)
            .ToListAsync(ct);

    private async Task ClearPrimaryAsync(Guid officeId, Guid documentId, CancellationToken ct)
        => await db.DocumentRelations
            .Where(x => x.OfficeId == officeId && x.DocumentId == documentId && x.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPrimary, false), ct);

    private async Task EnsureDocumentAsync(Guid officeId, Guid documentId, CancellationToken ct)
    {
        var ok = await db.Documents.AsNoTracking().AnyAsync(x => x.OfficeId == officeId && x.Id == documentId, ct);
        if (!ok)
        {
            throw new InvalidOperationException("Document not found in this office.");
        }
    }

    private async Task EnsureTargetAsync(Guid officeId, string targetType, Guid targetId, CancellationToken ct)
    {
        var ok = targetType switch
        {
            DocumentRelationTargets.Client => await db.Clients.AsNoTracking().AnyAsync(x => x.OfficeId == officeId && x.Id == targetId, ct),
            DocumentRelationTargets.Matter => await db.Matters.AsNoTracking().AnyAsync(x => x.OfficeId == officeId && x.Id == targetId, ct),
            DocumentRelationTargets.Party => await db.Parties.AsNoTracking().AnyAsync(x => x.OfficeId == officeId && x.Id == targetId, ct),
            _ => false,
        };
        if (!ok)
        {
            throw new InvalidOperationException($"{targetType} target not found in this office.");
        }
    }
}

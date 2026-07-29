using Korkut.Contract.Audit;
using Korkut.Core;
using Korkut.Contract.Documents;
using Korkut.Documents;
using Korkut.Documents.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Documents.Services;

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
public sealed class DocumentRelationService(DbContext db, IAuditLog audit, Korkut.Contract.Clients.IClientLookup clients, Korkut.Contract.Matters.IMatterLookup matters, IClock clock) : IDocumentRelationService
{
    /// <summary>
    /// Links a document to a target. Fails if either side is missing in this office.
    /// Re-linking the same pair updates the primary flag rather than duplicating.
    /// </summary>
    public async Task<Guid> LinkAsync(
        Guid officeId, Guid actorId, Guid documentId, string targetType, Guid targetId, bool isPrimary, CancellationToken ct = default)
    {
        if (!DocumentRelationTarget.IsValid(targetType))
        {
            throw new BadRequestException("Geçersiz bağlantı türü.");
        }

        // Authorization: both the document and the target must belong to this office.
        await EnsureDocumentAsync(officeId, documentId, ct);
        await EnsureTargetAsync(officeId, targetType, targetId, ct);

        if (isPrimary)
        {
            await ClearPrimaryAsync(officeId, documentId, ct);
        }

        var existing = await db.Set<DocumentRelation>().SingleOrDefaultAsync(
            x => x.OfficeId == officeId && x.DocumentId == documentId && x.TargetType == targetType && x.TargetId == targetId,
            ct);

        if (existing is not null)
        {
            var before = new { existing.IsPrimary };
            existing.SetPrimary(isPrimary);
            await db.SaveChangesAsync(ct);
            await audit.WriteAsync(new AuditEntry(
                officeId, actorId, null, null, "document.relation.updated", "document_relation", existing.Id,
                before, new { existing.IsPrimary }), ct);
            return existing.Id;
        }

        var relation = new DocumentRelation(
            officeId, documentId, targetType, targetId, isPrimary, actorId, clock.UtcNow);
        db.Set<DocumentRelation>().Add(relation);
        await db.SaveChangesAsync(ct);

        await audit.WriteAsync(new AuditEntry(
            officeId, actorId, null, null, "document.relation.linked", "document_relation", relation.Id,
            null, new { relation.DocumentId, relation.TargetType, relation.TargetId, relation.IsPrimary }), ct);
        return relation.Id;
    }

    /// <summary>Removes a link and records the removal in the audit trail.</summary>
    public async Task UnlinkAsync(Guid officeId, Guid actorId, Guid documentId, string targetType, Guid targetId, CancellationToken ct = default)
    {
        var relation = await db.Set<DocumentRelation>().SingleOrDefaultAsync(
            x => x.OfficeId == officeId && x.DocumentId == documentId && x.TargetType == targetType && x.TargetId == targetId,
            ct) ?? throw new NotFoundException("Bağlantı bulunamadı.");

        db.Set<DocumentRelation>().Remove(relation);
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
        => await db.Set<DocumentRelation>().AsNoTracking()
            .Where(x => x.OfficeId == officeId && x.TargetType == targetType && x.TargetId == targetId)
            .Select(x => x.DocumentId)
            .ToListAsync(ct);

    private async Task ClearPrimaryAsync(Guid officeId, Guid documentId, CancellationToken ct)
        => await db.Set<DocumentRelation>()
            .Where(x => x.OfficeId == officeId && x.DocumentId == documentId && x.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPrimary, false), ct);

    private async Task EnsureDocumentAsync(Guid officeId, Guid documentId, CancellationToken ct)
    {
        var ok = await db.Set<Document>().AsNoTracking().AnyAsync(x => x.OfficeId == officeId && x.Id == documentId, ct);
        if (!ok)
        {
            throw new NotFoundException("Belge bulunamadı.");
        }
    }

    private async Task EnsureTargetAsync(Guid officeId, string targetType, Guid targetId, CancellationToken ct)
    {
        var ok = targetType switch
        {
            DocumentRelationTarget.Client => await clients.ExistsAsync(officeId, targetId, ct),
            DocumentRelationTarget.Matter => await matters.ExistsAsync(officeId, targetId, ct),
            DocumentRelationTarget.Party => await matters.PartyExistsAsync(officeId, targetId, ct),
            _ => false,
        };
        if (!ok)
        {
            throw new NotFoundException("Bağlanacak kayıt bulunamadı.");
        }
    }
}

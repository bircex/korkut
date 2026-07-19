namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// A typed, auditable link between a document and another tenant entity
/// (client / matter / party). One relation may be marked the document's primary
/// context. Uniqueness over (office, document, target_type, target_id) makes
/// multi-linking idempotent; every link/unlink is recorded in the audit trail and
/// gated by an office-ownership authorization check (KDM-028).
/// </summary>
public sealed class DocumentRelationEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid DocumentId { get; set; }

    /// <summary>"client" | "matter" | "party".</summary>
    public string TargetType { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    /// <summary>The document's primary context — at most one per document.</summary>
    public bool IsPrimary { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

using Korkut.Core;

namespace Korkut.Documents;

/// <summary>
/// A document filed against a client, matter or party. The link is also how permission is
/// inherited: whoever may see the target may see the documents filed against it. At most
/// one relation per document is its primary context.
/// </summary>
public sealed class DocumentRelation : Entity, ITenantOwned, IAggregateRoot
{
    public DocumentRelation(
        Guid officeId, Guid documentId, string targetType, Guid targetId,
        bool isPrimary, Guid? createdBy, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        DocumentId = documentId;
        TargetType = targetType;
        TargetId = targetId;
        IsPrimary = isPrimary;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private DocumentRelation()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid DocumentId { get; private set; }

    /// <summary>"client" | "matter" | "party".</summary>
    public string TargetType { get; private set; } = string.Empty;

    public Guid TargetId { get; private set; }

    /// <summary>The document's primary context — at most one per document.</summary>
    public bool IsPrimary { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Re-filing the same pair only moves the primary flag; it never duplicates the link.</summary>
    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}

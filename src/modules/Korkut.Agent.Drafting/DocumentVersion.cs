using Korkut.Core;

namespace Korkut.Agent.Drafting;

/// <summary>An immutable snapshot of a generated document's content at accept or save time.</summary>
public sealed class DocumentVersion : Entity, ITenantOwned, IAggregateRoot
{
    public DocumentVersion(
        Guid officeId, Guid documentId, string contentMd, string? author, string? note, DateTimeOffset now)
        : base(NewId())
    {
        OfficeId = officeId;
        DocumentId = documentId;
        ContentMd = contentMd;
        Author = author;
        Note = note;
        CreatedAt = now;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private DocumentVersion()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid DocumentId { get; private set; }

    public string ContentMd { get; private set; } = string.Empty;

    /// <summary>Display attribution (the user's e-mail) for who produced this version.</summary>
    public string? Author { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

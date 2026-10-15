using Korkut.Core;

namespace Korkut.Correspondence.Entities;

/// <summary>An entry in the office's correspondence register ("evrak defteri").</summary>
public sealed class CorrespondenceItem : Entity, ITenantOwned, IAggregateRoot
{
    public CorrespondenceItem(
        Guid officeId, string direction, string title, string? referenceNo, DateOnly? docDate,
        Guid? matterId, string? notes, DateTimeOffset createdAt, Guid? createdBy = null)
        : base(NewId())
    {
        CreatedBy = createdBy;
        OfficeId = officeId;
        Direction = direction;
        Title = title;
        ReferenceNo = referenceNo;
        DocDate = docDate;
        MatterId = matterId;
        Notes = notes;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private CorrespondenceItem()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid? MatterId { get; private set; }

    /// <summary>"incoming" | "outgoing" | "internal".</summary>
    public string Direction { get; private set; } = "incoming";

    public string Title { get; private set; } = string.Empty;

    /// <summary>The register/reference number as printed on the document.</summary>
    public string? ReferenceNo { get; private set; }

    /// <summary>The date on the document itself, which may differ from when it was filed.</summary>
    public DateOnly? DocDate { get; private set; }

    public string? Notes { get; private set; }

    /// <summary>Who recorded it; an item with no matter is visible only to them (and to who sees every matter).</summary>
    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

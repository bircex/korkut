using Korkut.Domain.Common;

namespace Korkut.Domain.Matters;

/// <summary>
/// A tenant-owned case file / dosya. The court title is snapshotted onto the matter at link
/// time (<see cref="CourtNameSnapshot"/>) so a later court rename never rewrites the matter's
/// historical text. Retired by soft delete so linked documents keep their history.
/// </summary>
public sealed class Matter : Entity, ITenantOwned, ISoftDeletable, IAggregateRoot
{
    public Matter(
        Guid officeId, string title, Guid? clientId, Guid? courtId, string? courtNameSnapshot, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Title = title;
        ClientId = clientId;
        CourtId = courtId;
        CourtNameSnapshot = courtNameSnapshot;
        Status = "open";
        OpenedAt = createdAt;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private Matter()
    {
    }

    public Guid OfficeId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    /// <summary>Optional tenant-aware link to the client (composite FK on (office_id, client_id)).</summary>
    public Guid? ClientId { get; private set; }

    public string? FileNo { get; private set; }

    public string? CaseType { get; private set; }

    public string? Counterparty { get; private set; }

    /// <summary>Reference to a court row (whose title is mutable).</summary>
    public Guid? CourtId { get; private set; }

    /// <summary>IMMUTABLE snapshot of the court title at link time — never rewritten by a court rename.</summary>
    public string? CourtNameSnapshot { get; private set; }

    public string Status { get; private set; } = "open";

    public DateTimeOffset? OpenedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void Rename(string title) => Title = title;

    /// <summary>Closes the file; reporting counts open vs closed matters.</summary>
    public void Close() => Status = "closed";

    public void Reopen() => Status = "open";

    public void SoftDelete(DateTimeOffset deletedAt) => DeletedAt = deletedAt;
}

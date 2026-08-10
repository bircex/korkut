using Korkut.Core;

namespace Korkut.Matters.Entities;

/// <summary>
/// A tenant-owned case file / dosya. The court title is snapshotted onto the matter at link
/// time (<see cref="CourtNameSnapshot"/>) so a later court rename never rewrites the matter's
/// historical text. Retired by soft delete so linked documents keep their history.
/// </summary>
public sealed class Matter : Entity, ITenantOwned, ISoftDeletable, IAggregateRoot
{
    public Matter(
        Guid officeId, string title, Guid? clientId, string? clientNameSnapshot, string? fileNo,
        string? caseType, string? counterparty, Guid? courtId, string? courtNameSnapshot, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Title = title;
        ClientId = clientId;
        ClientNameSnapshot = clientNameSnapshot;
        FileNo = fileNo;
        CaseType = caseType;
        Counterparty = counterparty;
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

    /// <summary>Tenant-aware link to the client (composite FK on (office_id, client_id)). The matter
    /// is always bound by id — never by free-text name.</summary>
    public Guid? ClientId { get; private set; }

    /// <summary>IMMUTABLE snapshot of the client's name at bind time — never rewritten by a client rename.</summary>
    public string? ClientNameSnapshot { get; private set; }

    public string? FileNo { get; private set; }

    public string? CaseType { get; private set; }

    public string? Counterparty { get; private set; }

    /// <summary>Reference to a court row (whose title is mutable).</summary>
    public Guid? CourtId { get; private set; }

    /// <summary>IMMUTABLE snapshot of the court title at link time — never rewritten by a court rename.</summary>
    public string? CourtNameSnapshot { get; private set; }

    public string Status { get; private set; } = "open";

    public string? Notes { get; private set; }

    public DateTimeOffset? OpenedAt { get; private set; }

    /// <summary>Stamped on every edit; null until the matter is first updated.</summary>
    public DateTimeOffset? UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void Update(
        string? title, Guid? clientId, string? clientNameSnapshot, string? fileNo, string? caseType,
        string? counterparty, string? status, string? notes, DateTimeOffset updatedAt)
    {
        if (title is not null) Title = title;
        if (clientId is not null)
        {
            ClientId = clientId;
            ClientNameSnapshot = clientNameSnapshot;
        }
        FileNo = fileNo ?? FileNo;
        CaseType = caseType ?? CaseType;
        Counterparty = counterparty ?? Counterparty;
        if (status is not null) Status = status;
        if (notes is not null) Notes = notes;
        UpdatedAt = updatedAt;
    }

    public void Rename(string title) => Title = title;

    /// <summary>Closes the file; reporting counts open vs closed matters.</summary>
    public void Close() => Status = "closed";

    public void Reopen() => Status = "open";

    public void SoftDelete(DateTimeOffset deletedAt) => DeletedAt = deletedAt;
}

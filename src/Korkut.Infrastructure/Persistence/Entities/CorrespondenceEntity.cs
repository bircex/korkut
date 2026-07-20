namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// A tenant-owned entry in the correspondence register ("evrak defteri"): an inbound,
/// outbound, or internal official document logged against a matter. This is a REGISTER
/// row (metadata: title, reference number, date) — distinct from <see cref="DocumentEntity"/>,
/// which is a stored file in object storage.
/// </summary>
public sealed class CorrespondenceEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid? MatterId { get; set; }

    /// <summary>"incoming" | "outgoing" | "internal".</summary>
    public string Direction { get; set; } = "incoming";

    public string Title { get; set; } = string.Empty;

    /// <summary>Registry / reference number (e.g. tebligat or esas no).</summary>
    public string? ReferenceNo { get; set; }

    /// <summary>The document's own date (business date, no time-of-day).</summary>
    public DateOnly? DocDate { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

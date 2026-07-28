using Korkut.Core;

namespace Korkut.Jobs.Entities;

/// <summary>
/// A completed effect, remembered so a redelivery does not repeat it. The (office, key)
/// pair is the identity — and the table's primary key — which is what makes recording an
/// effect twice impossible rather than merely unlikely.
/// </summary>
public sealed class InboxEntry : ITenantOwned, IAggregateRoot
{
    public InboxEntry(Guid officeId, string idempotencyKey, DateTimeOffset processedAt)
    {
        OfficeId = officeId;
        IdempotencyKey = idempotencyKey;
        ProcessedAt = processedAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private InboxEntry()
    {
    }

    public Guid OfficeId { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public DateTimeOffset ProcessedAt { get; private set; }
}

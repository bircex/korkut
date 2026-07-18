namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>Marker for tenant-owned rows: every one carries its owning office.</summary>
public interface ITenantOwned
{
    Guid OfficeId { get; }
}

/// <summary>The tenant root (an office/firm). Not itself RLS-scoped — access is controlled separately.</summary>
public sealed class OfficeEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A tenant-owned case file (minimal here; KDM-023 adds the full domain). Carries
/// <c>office_id</c> and a composite unique <c>(office_id, id)</c> so children can
/// reference it with a tenant-aware composite FK.
/// </summary>
public sealed class MatterEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// A tenant-owned child of a matter. Its FK is composite — <c>(office_id, matter_id)</c>
/// → <c>matters(office_id, id)</c> — so a child can never point at a parent in a
/// different office (cross-tenant link is rejected by the database).
/// </summary>
public sealed class MatterHearingEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid MatterId { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }
}

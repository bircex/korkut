namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>Marker for tenant-owned rows: every one carries its owning office.</summary>
public interface ITenantOwned
{
    Guid OfficeId { get; }
}

/// <summary>Marker for rows with a surrogate primary key (enables generic key-based repository ops).</summary>
public interface IIdentifiable
{
    Guid Id { get; }
}

/// <summary>The tenant root (an office/firm). Not itself RLS-scoped — access is controlled separately.</summary>
public sealed class OfficeEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>A disabled office cannot sign in (demo-approval / suspension gate).</summary>
    public bool Disabled { get; set; }

    /// <summary>When true, every office user must complete Korkut TOTP at login (KDM-017).</summary>
    public bool RequireTwoFactor { get; set; }

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

    /// <summary>Optional tenant-aware link to the client (composite FK on (office_id, client_id)).</summary>
    public Guid? ClientId { get; set; }

    public string? FileNo { get; set; }

    public string? CaseType { get; set; }

    public string? Counterparty { get; set; }

    /// <summary>Reference to a court row (mutable title).</summary>
    public Guid? CourtId { get; set; }

    /// <summary>IMMUTABLE snapshot of the court title at link time — never rewritten by a court rename.</summary>
    public string? CourtNameSnapshot { get; set; }

    public string Status { get; set; } = "open";

    public DateTimeOffset? OpenedAt { get; set; }

    /// <summary>Soft-delete for retention; excluded from normal listings.</summary>
    public DateTimeOffset? DeletedAt { get; set; }

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

    /// <summary>The hearing instant (date + optional time). Consumed by the calendar/agenda.</summary>
    public DateTimeOffset ScheduledAt { get; set; }

    /// <summary>Hearing type (e.g. "ön inceleme", "tahkikat") — Python parity.</summary>
    public string? Type { get; set; }

    public string? Location { get; set; }

    /// <summary>Outcome recorded after the hearing.</summary>
    public string? Result { get; set; }

    public string? Notes { get; set; }

    /// <summary>"pending" | "sent" | "dismissed".</summary>
    public string ReminderStatus { get; set; } = "pending";
}

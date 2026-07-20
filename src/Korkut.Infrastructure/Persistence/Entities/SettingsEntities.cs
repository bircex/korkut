namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// The office-wide AI persona (one row per office): how the assistant should sound and
/// what it should assume. Office-level only — there is no per-user persona.
/// </summary>
public sealed class PersonaEntity : ITenantOwned
{
    /// <summary>Also the primary key — exactly one persona row per office.</summary>
    public Guid OfficeId { get; set; }

    public string Tone { get; set; } = string.Empty;

    public string Expertise { get; set; } = string.Empty;

    public string ExtraRules { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// A drafting style profile. Each office has one office-level profile (<see cref="Scope"/>
/// = "office") and each user may have one personal profile ("user"). The effective
/// style overlays the user's non-empty fields on top of the office defaults.
/// </summary>
public sealed class StyleProfileEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>"office" | "user".</summary>
    public string Scope { get; set; } = "office";

    /// <summary>The owning user for a "user" profile; <see cref="Guid.Empty"/> for the office profile.</summary>
    public Guid UserId { get; set; }

    public string Letterhead { get; set; } = string.Empty;

    public string Signature { get; set; } = string.Empty;

    public string Tone { get; set; } = string.Empty;

    public string Boilerplate { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// A remembered fact/instruction for the office assistant. Member-added items start
/// "pending" and an owner promotes them to "active"; owner-added items are "active".
/// </summary>
public sealed class MemoryItemEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>Free-form category, e.g. "client" | "court" | "preference".</summary>
    public string Kind { get; set; } = "note";

    public string Text { get; set; } = string.Empty;

    /// <summary>"active" | "pending".</summary>
    public string Status { get; set; } = "pending";

    /// <summary>The user who added it (for attribution).</summary>
    public Guid? AuthorId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

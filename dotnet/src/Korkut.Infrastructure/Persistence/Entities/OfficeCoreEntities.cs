namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>A tenant-owned client (individual or corporate). Soft-deleted for retention.</summary>
public sealed class ClientEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>"individual" | "corporate".</summary>
    public string Kind { get; set; } = "individual";

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Reference court data with a MUTABLE display title. Matters snapshot the title at
/// link time so a later rename never rewrites historical documents (KDM-023).
/// </summary>
public sealed class CourtEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>Mutable display title — may be corrected over time.</summary>
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A party/counsel attached to a matter (tenant-owned; composite FK to the matter).</summary>
public sealed class PartyEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    public Guid MatterId { get; set; }

    /// <summary>"plaintiff" | "defendant" | "counsel" | ...</summary>
    public string Role { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

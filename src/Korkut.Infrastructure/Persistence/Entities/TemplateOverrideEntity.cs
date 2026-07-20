namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// An office- or user-level override of a built-in document template. The built-in
/// defaults ship as static catalog content (petition/contract examples); an override
/// stores custom markdown that takes precedence. Effective content is
/// user-override → office-override → built-in default.
/// </summary>
public sealed class TemplateOverrideEntity : ITenantOwned
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>"office" | "user".</summary>
    public string Scope { get; set; } = "office";

    /// <summary>The owning user for a "user" override; <see cref="Guid.Empty"/> for the office override.</summary>
    public Guid UserId { get; set; }

    /// <summary>"dilekce" | "sozlesme".</summary>
    public string Kind { get; set; } = "dilekce";

    /// <summary>The catalog type id this overrides (kebab-case, e.g. "anlasmali-bosanma").</summary>
    public string TypeId { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; set; }
}

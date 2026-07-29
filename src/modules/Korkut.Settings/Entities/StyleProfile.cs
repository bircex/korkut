using Korkut.Core;

namespace Korkut.Settings.Entities;

/// <summary>
/// A drafting style profile. Each office has one office-level profile and each user may
/// have one personal profile; the effective style overlays the user's non-empty fields on
/// top of the office defaults (see <see cref="StyleFields.Over"/>).
/// </summary>
public sealed class StyleProfile : Entity, ITenantOwned, IAggregateRoot
{
    public StyleProfile(Guid officeId, string scope, Guid userId)
        : base(NewId())
    {
        OfficeId = officeId;
        Scope = scope;
        UserId = userId;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private StyleProfile()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>"office" | "user".</summary>
    public string Scope { get; private set; } = StyleScope.Office;

    /// <summary>The owning user for a "user" profile; <see cref="Guid.Empty"/> for the office profile.</summary>
    public Guid UserId { get; private set; }

    public string Letterhead { get; private set; } = string.Empty;

    public string Signature { get; private set; } = string.Empty;

    public string Tone { get; private set; } = string.Empty;

    public string Boilerplate { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>This layer's fields, as the value the overlay works on.</summary>
    public StyleFields Fields => new(Letterhead, Signature, Tone, Boilerplate);

    public void Apply(StyleFields fields, DateTimeOffset updatedAt)
    {
        Letterhead = fields.Letterhead;
        Signature = fields.Signature;
        Tone = fields.Tone;
        Boilerplate = fields.Boilerplate;
        UpdatedAt = updatedAt;
    }
}

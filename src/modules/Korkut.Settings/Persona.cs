using Korkut.Core;

namespace Korkut.Settings;

/// <summary>
/// The office-wide AI persona: how the assistant should sound and what it should assume.
/// Office-level only — there is no per-user persona, so the office IS the identity and
/// there is exactly one row per office.
/// </summary>
/// <remarks>
/// Keyed by the office rather than a surrogate id (as the table always was), so this
/// aggregate does not derive from <see cref="Entity"/> and its port is written out instead
/// of <c>IRepository&lt;T&gt;</c>.
/// </remarks>
public sealed class Persona : ITenantOwned, IAggregateRoot
{
    public Persona(Guid officeId) => OfficeId = officeId;

    /// <summary>Materialization constructor for the ORM.</summary>
    private Persona()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>How the assistant should sound.</summary>
    public string Tone { get; private set; } = string.Empty;

    /// <summary>The office's field of practice, so the assistant knows what to assume.</summary>
    public string Expertise { get; private set; } = string.Empty;

    /// <summary>Anything else the office wants every answer to respect.</summary>
    public string ExtraRules { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string tone, string expertise, string extraRules, DateTimeOffset updatedAt)
    {
        Tone = tone;
        Expertise = expertise;
        ExtraRules = extraRules;
        UpdatedAt = updatedAt;
    }
}

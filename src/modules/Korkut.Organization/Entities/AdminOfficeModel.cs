using Korkut.Core;

namespace Korkut.Organization.Entities;

/// <summary>
/// The admin console's per-office model assignment (catalog key + enabled flag). API keys never
/// live here — only a reference into the DB model catalog (<see cref="ModelCatalogEntry"/>). When
/// enabled it is the TOP rung of the routing chain: this office uses this model, ahead of the
/// platform default and the env specialty router (see
/// Korkut.Contract.Agent.Model.IOfficeModelOverrideService, implemented by OfficeModelResolver).
/// </summary>
public sealed class AdminOfficeModel : Entity, IAggregateRoot
{
    public AdminOfficeModel(Guid officeId, string? modelKey, bool enabled, DateTimeOffset now)
        : base(NewId())
    {
        OfficeId = officeId;
        ModelKey = modelKey;
        Enabled = enabled;
        UpdatedAt = now;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private AdminOfficeModel()
    {
    }

    public Guid OfficeId { get; private set; }

    public string? ModelKey { get; private set; }

    public bool Enabled { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Assign(string? modelKey, bool enabled, DateTimeOffset now)
    {
        ModelKey = modelKey;
        Enabled = enabled;
        UpdatedAt = now;
    }
}

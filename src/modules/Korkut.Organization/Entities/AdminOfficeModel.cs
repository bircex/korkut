using Korkut.Core;

namespace Korkut.Organization.Entities;

/// <summary>
/// The admin console's per-office model assignment (catalog key + enabled flag). Assignment
/// only — API keys never live here, and the runtime router does not consume this yet (it is
/// the operator's bookkeeping surface, mirrored from the pre-.NET console).
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

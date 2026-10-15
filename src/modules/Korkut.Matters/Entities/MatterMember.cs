using Korkut.Core;

namespace Korkut.Matters.Entities;

/// <summary>
/// A member of a matter's team. Being on the team is what lets a member see the matter and
/// everything tied to it; the lead (sorumlu avukat) may also change the team.
/// </summary>
public sealed class MatterMember : ITenantOwned
{
    public MatterMember(Guid officeId, Guid matterId, Guid userId, bool isLead, Guid? addedBy, DateTimeOffset addedAt)
    {
        OfficeId = officeId;
        MatterId = matterId;
        UserId = userId;
        IsLead = isLead;
        AddedBy = addedBy;
        AddedAt = addedAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private MatterMember()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid MatterId { get; private set; }

    public Guid UserId { get; private set; }

    public bool IsLead { get; private set; }

    public Guid? AddedBy { get; private set; }

    public DateTimeOffset AddedAt { get; private set; }

    public void SetLead(bool isLead) => IsLead = isLead;
}

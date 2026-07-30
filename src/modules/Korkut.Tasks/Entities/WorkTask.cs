using Korkut.Core;

namespace Korkut.Tasks.Entities;

/// <summary>
/// A unit of office work, optionally attached to a matter and an assignee. Named
/// <c>WorkTask</c> so it never collides with <see cref="System.Threading.Tasks.Task"/>.
/// </summary>
public sealed class WorkTask : Entity, ITenantOwned, IAggregateRoot
{
    public WorkTask(
        Guid officeId, string title, string? description, Guid? matterId, Guid? assigneeId,
        DateTimeOffset? dueAt, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Title = title;
        Description = description;
        MatterId = matterId;
        AssigneeId = assigneeId;
        DueAt = dueAt;
        Status = "open";
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private WorkTask()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid? MatterId { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    /// <summary>"open" | "in_progress" | "done".</summary>
    public string Status { get; private set; } = "open";

    public DateTimeOffset? DueAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Moving to "done" stamps the completion time; moving away clears it.</summary>
    public void SetStatus(string status, DateTimeOffset now)
    {
        Status = status;
        CompletedAt = status == "done" ? now : null;
    }

    public void Reassign(Guid? assigneeId) => AssigneeId = assigneeId;
}

using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Tasks;

/// <summary>Body for a task PATCH: move it through its lifecycle and/or reassign it. Both fields are
/// optional — only the ones sent are applied.</summary>
public sealed record UpdateTaskStatusRequest
{
    [RegularExpression("open|in_progress|done", ErrorMessage = "status 'open', 'in_progress' veya 'done' olmalıdır.")]
    public string? Status { get; init; }

    /// <summary>New assignee; when present, reassigns the task.</summary>
    public Guid? AssigneeId { get; init; }
}

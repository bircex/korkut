using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Tasks.Requests;

/// <summary>Body for creating a task.</summary>
public sealed record CreateTaskRequest
{
    [Required]
    [MaxLength(500)]
    public string? Title { get; init; }

    public Guid? MatterId { get; init; }

    public Guid? AssigneeId { get; init; }

    public DateTimeOffset? DueAt { get; init; }
}

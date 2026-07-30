using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Tasks;

/// <summary>Body for creating a task.</summary>
public sealed record CreateTaskRequest
{
    [Required]
    [MaxLength(500)]
    public string? Title { get; init; }

    public Guid? MatterId { get; init; }

    public Guid? AssigneeId { get; init; }

    public DateTimeOffset? DueDate { get; init; }
}

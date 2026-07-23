using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Tasks.Requests;

/// <summary>Body for moving a task through its lifecycle.</summary>
public sealed record UpdateTaskStatusRequest
{
    [Required]
    [RegularExpression("open|in_progress|done", ErrorMessage = "status 'open', 'in_progress' veya 'done' olmalıdır.")]
    public string? Status { get; init; }
}

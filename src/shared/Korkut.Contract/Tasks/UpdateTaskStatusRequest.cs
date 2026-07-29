using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Tasks;

/// <summary>Body for moving a task through its lifecycle.</summary>
public sealed record UpdateTaskStatusRequest
{
    [Required]
    [RegularExpression("open|in_progress|done", ErrorMessage = "status 'open', 'in_progress' veya 'done' olmalıdır.")]
    public string? Status { get; init; }
}

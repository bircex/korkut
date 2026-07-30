using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Tasks;

/// <summary>Partial update of an agenda record; every omitted (null) field keeps its value. Moving
/// <see cref="Status"/> to "done" stamps the completion time, moving it back clears it.</summary>
public sealed record UpdateTaskRequest
{
    [RegularExpression("gorev|hatirlatma|randevu", ErrorMessage = "category 'gorev', 'hatirlatma' veya 'randevu' olmalıdır.")]
    public string? Category { get; init; }

    [MaxLength(500)]
    public string? Title { get; init; }

    [MaxLength(4000)]
    public string? Description { get; init; }

    [MaxLength(100)]
    public string? Type { get; init; }

    [RegularExpression("open|in_progress|done", ErrorMessage = "status 'open', 'in_progress' veya 'done' olmalıdır.")]
    public string? Status { get; init; }

    public Guid? MatterId { get; init; }

    /// <summary>New assignee; when present, reassigns the record.</summary>
    public Guid? AssigneeId { get; init; }

    public Guid? ClientId { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    public DateTimeOffset? EndAt { get; init; }

    public bool? AllDay { get; init; }

    [MaxLength(400)]
    public string? Location { get; init; }

    [MaxLength(400)]
    public string? Rrule { get; init; }

    [Range(0, 40320)]
    public int? RemindBeforeMin { get; init; }
}

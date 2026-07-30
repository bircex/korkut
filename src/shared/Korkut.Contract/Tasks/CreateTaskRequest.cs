using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Tasks;

/// <summary>Body for creating an agenda record (görev / hatırlatma / randevu).</summary>
public sealed record CreateTaskRequest
{
    /// <summary>"gorev" (default when null) | "hatirlatma" | "randevu".</summary>
    [RegularExpression("gorev|hatirlatma|randevu", ErrorMessage = "category 'gorev', 'hatirlatma' veya 'randevu' olmalıdır.")]
    public string? Category { get; init; }

    [Required]
    [MaxLength(500)]
    public string? Title { get; init; }

    [MaxLength(4000)]
    public string? Description { get; init; }

    [MaxLength(100)]
    public string? Type { get; init; }

    public Guid? MatterId { get; init; }

    public Guid? AssigneeId { get; init; }

    public Guid? ClientId { get; init; }

    /// <summary>The scheduled point ("when"): the date for all-day items, or the start time when timed.</summary>
    public DateTimeOffset? DueDate { get; init; }

    public DateTimeOffset? EndAt { get; init; }

    /// <summary>Defaults to true — a plain to-do sits all-day on its due date.</summary>
    public bool AllDay { get; init; } = true;

    [MaxLength(400)]
    public string? Location { get; init; }

    [MaxLength(400)]
    public string? Rrule { get; init; }

    [Range(0, 40320)] // up to 4 weeks
    public int? RemindBeforeMin { get; init; }
}

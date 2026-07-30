using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Deadlines;

/// <summary>Body for creating a deadline.</summary>
public sealed record CreateDeadlineRequest
{
    [Required]
    [MaxLength(500)]
    public string? Title { get; init; }

    [Required]
    public DateOnly DueDate { get; init; }

    /// <summary>Free-text label (the UI's "type"); optional.</summary>
    [MaxLength(100)]
    public string? Type { get; init; } = "internal";

    [MaxLength(4000)]
    public string? Notes { get; init; }

    public Guid? MatterId { get; init; }

    public string TimeZoneId { get; init; } = "Europe/Istanbul";

    [Range(0, 365)]
    public int ReminderDaysBefore { get; init; }
}

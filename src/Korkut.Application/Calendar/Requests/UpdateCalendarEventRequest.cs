using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Calendar.Requests;

/// <summary>Partial update of an editable calendar event; omitted fields keep their value.</summary>
public sealed record UpdateCalendarEventRequest
{
    [MaxLength(500)]
    public string? Title { get; init; }

    [MaxLength(100)]
    public string? Type { get; init; }

    public DateTimeOffset? StartAt { get; init; }

    public DateTimeOffset? EndAt { get; init; }

    public bool? AllDay { get; init; }

    public Guid? MatterId { get; init; }

    public Guid? ClientId { get; init; }

    public Guid? AssigneeId { get; init; }

    [MaxLength(400)]
    public string? Location { get; init; }

    [RegularExpression("scheduled|done|cancelled")]
    public string? Status { get; init; }

    [MaxLength(1000)]
    public string? Result { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }

    [MaxLength(400)]
    public string? Rrule { get; init; }

    [Range(0, 40320)]
    public int? RemindBeforeMin { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Notes.Requests;

/// <summary>Body for recording a communication note.</summary>
public sealed record CreateNoteRequest
{
    [Required]
    [MaxLength(4000)]
    public string? Body { get; init; }

    public string Channel { get; init; } = "note";

    public Guid? MatterId { get; init; }

    public Guid? ClientId { get; init; }

    public DateTimeOffset? OccurredAt { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Hearings.Requests;

/// <summary>Body for adding a hearing to a matter.</summary>
public sealed record CreateHearingRequest
{
    /// <summary>Hearing date (required). Combined with <see cref="Time"/> into the scheduled instant.</summary>
    [Required(ErrorMessage = "Duruşma tarihi zorunludur.")]
    public DateOnly? Date { get; init; }

    /// <summary>Optional time of day; when omitted the hearing is scheduled at midnight (UTC).</summary>
    public TimeOnly? Time { get; init; }

    [MaxLength(100)]
    public string? Type { get; init; }

    [MaxLength(300)]
    public string? Location { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }
}

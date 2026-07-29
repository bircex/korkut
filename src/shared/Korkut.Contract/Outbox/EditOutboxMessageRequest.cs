using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Outbox;

/// <summary>Body for editing a draft before it is confirmed.</summary>
public sealed record EditOutboxMessageRequest
{
    [Required]
    [MaxLength(400)]
    public string? ToAddr { get; init; }

    [MaxLength(500)]
    public string Subject { get; init; } = string.Empty;

    [Required]
    public string? Body { get; init; }

    public DateTimeOffset? SendAt { get; init; }
}

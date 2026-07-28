using System.ComponentModel.DataAnnotations;

namespace Korkut.Agent.Chat;

public sealed record ChatRequest
{
    [Required, MaxLength(40000)]
    public string? Text { get; init; }
}

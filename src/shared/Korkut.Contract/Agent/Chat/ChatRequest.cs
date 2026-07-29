using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Agent.Chat;

public sealed record ChatRequest
{
    [Required, MaxLength(40000)]
    public string? Text { get; init; }
}

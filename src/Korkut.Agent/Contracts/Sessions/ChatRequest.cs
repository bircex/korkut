using System.ComponentModel.DataAnnotations;

namespace Korkut.Agent.Contracts.Sessions;

public sealed record ChatRequest
{
    [Required, MaxLength(40000)]
    public string? Text { get; init; }
}

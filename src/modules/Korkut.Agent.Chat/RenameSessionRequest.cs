using System.ComponentModel.DataAnnotations;

namespace Korkut.Agent.Chat;

public sealed record RenameSessionRequest
{
    [Required, MaxLength(300)]
    public string? Title { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace Korkut.Agent.Contracts.Sessions;

public sealed record RenameSessionRequest
{
    [Required, MaxLength(300)]
    public string? Title { get; init; }
}

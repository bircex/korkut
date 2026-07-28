using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Agent.Drafting;

public sealed record DraftRequest
{
    [Required, MaxLength(4000)]
    public string? Instruction { get; init; }

    [MaxLength(100)]
    public string? PetitionType { get; init; }
}

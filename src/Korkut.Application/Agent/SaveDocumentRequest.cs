using System.ComponentModel.DataAnnotations;

namespace Korkut.Agent.Contracts.Documents;

public sealed record SaveDocumentRequest
{
    [Required, MaxLength(300)]
    public string? Title { get; init; }

    [Required]
    public string? ContentMd { get; init; }
}

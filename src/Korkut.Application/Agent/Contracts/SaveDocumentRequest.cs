using System.ComponentModel.DataAnnotations;
using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;

namespace Korkut.Application.Agent.Contracts;

public sealed record SaveDocumentRequest
{
    [Required, MaxLength(300)]
    public string? Title { get; init; }

    [Required]
    public string? ContentMd { get; init; }
}

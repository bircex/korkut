using System.ComponentModel.DataAnnotations;
using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;

namespace Korkut.Application.Agent.Contracts;

public sealed record ChatRequest
{
    [Required, MaxLength(40000)]
    public string? Text { get; init; }
}

using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Settings.Requests;

/// <summary>Body for setting the office persona.</summary>
public sealed record SavePersonaRequest
{
    [MaxLength(2000, ErrorMessage = "Üslup en fazla 2000 karakter olabilir.")]
    public string Tone { get; init; } = string.Empty;

    [MaxLength(2000, ErrorMessage = "Uzmanlık en fazla 2000 karakter olabilir.")]
    public string Expertise { get; init; } = string.Empty;

    [MaxLength(4000, ErrorMessage = "Ek kurallar en fazla 4000 karakter olabilir.")]
    public string ExtraRules { get; init; } = string.Empty;
}

using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Settings.Requests;

/// <summary>Body for setting one drafting-style layer; an omitted field clears it at that layer.</summary>
public sealed record SaveStyleRequest
{
    [MaxLength(2000, ErrorMessage = "Antet en fazla 2000 karakter olabilir.")]
    public string? Letterhead { get; init; }

    [MaxLength(2000, ErrorMessage = "İmza en fazla 2000 karakter olabilir.")]
    public string? Signature { get; init; }

    [MaxLength(2000, ErrorMessage = "Üslup en fazla 2000 karakter olabilir.")]
    public string? Tone { get; init; }

    [MaxLength(4000, ErrorMessage = "Standart metin en fazla 4000 karakter olabilir.")]
    public string? Boilerplate { get; init; }
}

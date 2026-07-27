using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Settings;

/// <summary>The office persona, with whether this caller may change it.</summary>
public sealed record PersonaResponse(string Tone, string Expertise, string ExtraRules, bool CanEdit);

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

/// <summary>Office-persona use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface IPersonaService
{
    Task<Result<PersonaResponse>> GetAsync(CancellationToken ct = default);

    Task<Result<PersonaResponse>> SaveAsync(SavePersonaRequest request, CancellationToken ct = default);
}

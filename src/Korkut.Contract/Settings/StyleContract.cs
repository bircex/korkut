using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Settings;

/// <summary>One drafting-style layer as the client sees it.</summary>
public sealed record StyleFieldsResponse(string Letterhead, string Signature, string Tone, string Boilerplate);

/// <summary>Both style layers plus the overlay the assistant drafts with, and whether the caller may change the office layer.</summary>
public sealed record StyleResponse(
    StyleFieldsResponse Office, StyleFieldsResponse User, StyleFieldsResponse Effective, bool CanEditOffice);

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

/// <summary>Drafting-style use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface IStyleService
{
    Task<Result<StyleResponse>> GetAsync(CancellationToken ct = default);

    Task<Result<StyleFieldsResponse>> SaveUserAsync(SaveStyleRequest request, CancellationToken ct = default);

    Task<Result<StyleFieldsResponse>> SaveOfficeAsync(SaveStyleRequest request, CancellationToken ct = default);
}

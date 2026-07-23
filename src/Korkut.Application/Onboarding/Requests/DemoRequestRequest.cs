using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Onboarding.Requests;

/// <summary>Body for the self-serve signup: the office the visitor wants created.</summary>
public sealed record DemoRequestRequest
{
    [Required(ErrorMessage = "Büro adı zorunludur.")]
    [MaxLength(200, ErrorMessage = "Büro adı en fazla 200 karakter olabilir.")]
    public string? OfficeName { get; init; }

    /// <summary>The requester's name (captured for the record; optional).</summary>
    [MaxLength(200, ErrorMessage = "Ad en fazla 200 karakter olabilir.")]
    public string? FullName { get; init; }
}

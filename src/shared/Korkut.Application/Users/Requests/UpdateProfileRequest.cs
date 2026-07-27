using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Users.Requests;

/// <summary>Body for the caller updating their own profile.</summary>
public sealed record UpdateProfileRequest
{
    [MaxLength(200, ErrorMessage = "Ad en fazla 200 karakter olabilir.")]
    public string? FullName { get; init; }

    [MaxLength(50, ErrorMessage = "Baro sicil no en fazla 50 karakter olabilir.")]
    public string? BarRegistryNo { get; init; }
}

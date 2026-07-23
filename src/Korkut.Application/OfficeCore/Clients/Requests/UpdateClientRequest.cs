using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.OfficeCore.Clients.Requests;

/// <summary>Body for updating a client.</summary>
public sealed record UpdateClientRequest
{
    [Required(ErrorMessage = "Ad zorunludur.")]
    [MaxLength(300, ErrorMessage = "Ad en fazla 300 karakter olabilir.")]
    public string? Name { get; init; }

    [RegularExpression("individual|corporate", ErrorMessage = "Tür 'individual' veya 'corporate' olmalıdır.")]
    public string Kind { get; init; } = "individual";

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }

    [MaxLength(50)]
    public string? Phone { get; init; }
}

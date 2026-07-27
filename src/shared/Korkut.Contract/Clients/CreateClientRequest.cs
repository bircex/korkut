using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Clients;

/// <summary>Body for creating a client.</summary>
public sealed record CreateClientRequest
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

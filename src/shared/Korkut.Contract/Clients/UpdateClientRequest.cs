using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Clients;

/// <summary>Body for updating a client.</summary>
public sealed record UpdateClientRequest
{
    [Required(ErrorMessage = "Ad zorunludur.")]
    [MaxLength(400, ErrorMessage = "Ad en fazla 400 karakter olabilir.")]
    public string? FullName { get; init; }

    [RegularExpression("individual|corporate", ErrorMessage = "Tür 'individual' veya 'corporate' olmalıdır.")]
    public string Kind { get; init; } = "individual";

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta girin.")]
    public string? Email { get; init; }

    [MaxLength(50)]
    public string? Phone { get; init; }

    [MaxLength(20, ErrorMessage = "Kimlik numarası en fazla 20 karakter olabilir.")]
    public string? NationalId { get; init; }

    [MaxLength(500, ErrorMessage = "Adres en fazla 500 karakter olabilir.")]
    public string? Address { get; init; }

    public DateOnly? DateOfBirth { get; init; }
}

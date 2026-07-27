using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Matters;

/// <summary>Body for opening a matter.</summary>
public sealed record CreateMatterRequest
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(500, ErrorMessage = "Başlık en fazla 500 karakter olabilir.")]
    public string? Title { get; init; }

    public Guid? ClientId { get; init; }

    public Guid? CourtId { get; init; }
}

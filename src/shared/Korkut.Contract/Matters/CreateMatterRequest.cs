using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Matters;

/// <summary>Body for opening a matter. The client is bound by id (resolved from the office's
/// existing client list) — free-text client names are not accepted. The court may still be
/// free-text or linked by <see cref="CourtId"/>.</summary>
public sealed record CreateMatterRequest
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(500, ErrorMessage = "Başlık en fazla 500 karakter olabilir.")]
    public string? Title { get; init; }

    public Guid? ClientId { get; init; }

    [MaxLength(100)]
    public string? FileNo { get; init; }

    [MaxLength(400)]
    public string? Court { get; init; }

    public Guid? CourtId { get; init; }

    [MaxLength(100)]
    public string? CaseType { get; init; }

    [MaxLength(400)]
    public string? Counterparty { get; init; }
}

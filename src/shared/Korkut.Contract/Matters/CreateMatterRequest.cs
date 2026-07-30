using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Matters;

/// <summary>Body for opening a matter. The UI works with free-text client/court names; an internal
/// caller (agent tool) may instead pass <see cref="ClientId"/>/<see cref="CourtId"/> to link records.</summary>
public sealed record CreateMatterRequest
{
    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(500, ErrorMessage = "Başlık en fazla 500 karakter olabilir.")]
    public string? Title { get; init; }

    [MaxLength(400)]
    public string? ClientName { get; init; }

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

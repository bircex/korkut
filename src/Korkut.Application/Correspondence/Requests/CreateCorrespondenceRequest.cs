using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Correspondence.Requests;

/// <summary>Body for filing a correspondence-register entry.</summary>
public sealed record CreateCorrespondenceRequest
{
    [Required]
    [RegularExpression("incoming|outgoing|internal", ErrorMessage = "direction 'incoming', 'outgoing' veya 'internal' olmalıdır.")]
    public string? Direction { get; init; }

    [Required]
    [MaxLength(500)]
    public string? Title { get; init; }

    [MaxLength(100)]
    public string? ReferenceNo { get; init; }

    public DateOnly? DocDate { get; init; }

    public Guid? MatterId { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }
}

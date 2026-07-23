using System.ComponentModel.DataAnnotations;

namespace Korkut.Application.Settings.Requests;

/// <summary>Body for adding a memory item.</summary>
public sealed record CreateMemoryRequest
{
    [Required(ErrorMessage = "Metin zorunludur.")]
    [MaxLength(4000, ErrorMessage = "Metin en fazla 4000 karakter olabilir.")]
    public string? Text { get; init; }

    [MaxLength(50, ErrorMessage = "Tür en fazla 50 karakter olabilir.")]
    public string Kind { get; init; } = "note";
}

using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Matters;

/// <summary>Body for editing a matter (all fields optional — only the ones sent are changed).</summary>
public sealed record UpdateMatterRequest
{
    [MaxLength(500, ErrorMessage = "Başlık en fazla 500 karakter olabilir.")]
    public string? Title { get; init; }

    /// <summary>Rebinds the matter to a different client (or attaches one for the first time).</summary>
    public Guid? ClientId { get; init; }

    public string? FileNo { get; init; }

    public string? CaseType { get; init; }

    public string? Counterparty { get; init; }

    public string? Status { get; init; }

    [MaxLength(8000)]
    public string? Notes { get; init; }
}

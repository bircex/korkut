using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Documents.Requests;

/// <summary>Body for filing a document against a client, matter or party (or removing that link).</summary>
public sealed record LinkDocumentRequest
{
    [Required(ErrorMessage = "target_type zorunludur.")]
    [RegularExpression("client|matter|party", ErrorMessage = "target_type 'client', 'matter' veya 'party' olmalıdır.")]
    public string? TargetType { get; init; }

    [Required(ErrorMessage = "target_id zorunludur.")]
    public Guid TargetId { get; init; }

    /// <summary>Make this the document's primary context, replacing any previous one.</summary>
    public bool IsPrimary { get; init; }
}

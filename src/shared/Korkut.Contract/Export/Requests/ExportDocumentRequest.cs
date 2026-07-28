using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Export.Requests;

/// <summary>Body for exporting caller-supplied markdown to a file.</summary>
public sealed record ExportDocumentRequest
{
    [Required(ErrorMessage = "Biçim zorunludur.")]
    [RegularExpression("docx|udf", ErrorMessage = "format 'docx' veya 'udf' olmalıdır.")]
    public string? Format { get; init; }

    [Required(ErrorMessage = "Başlık zorunludur.")]
    [MaxLength(300, ErrorMessage = "Başlık en fazla 300 karakter olabilir.")]
    public string? Title { get; init; }

    [Required(ErrorMessage = "İçerik zorunludur.")]
    public string? Markdown { get; init; }
}

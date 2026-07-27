namespace Korkut.Application.Documents.OCR;

/// <summary>
/// Extracts text spans from a document's bytes. Each implementation carries a stable
/// <see cref="ParserVersion"/> so a parser change produces a NEW immutable artifact
/// version rather than overwriting the old one (KDM-027). Production adds a Tesseract
/// OCR extractor + a PDF text-layer extractor behind this same contract.
/// </summary>
public interface ITextExtractorService
{
    string ParserVersion { get; }

    Task<IReadOnlyList<TextExtractorServiceResult>> ExtractAsync(Stream content, string contentType, CancellationToken ct = default);
}

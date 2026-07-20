namespace Korkut.Application.Documents;

/// <summary>One extracted span with its page + order, for provenance-tracked projections.</summary>
public sealed record ExtractedSpan(int Page, int Index, string Text);

/// <summary>
/// Extracts text spans from a document's bytes. Each implementation carries a stable
/// <see cref="ParserVersion"/> so a parser change produces a NEW immutable artifact
/// version rather than overwriting the old one (KDM-027). Production adds a Tesseract
/// OCR extractor + a PDF text-layer extractor behind this same contract.
/// </summary>
public interface ITextExtractor
{
    string ParserVersion { get; }

    Task<IReadOnlyList<ExtractedSpan>> ExtractAsync(Stream content, string contentType, CancellationToken ct = default);
}

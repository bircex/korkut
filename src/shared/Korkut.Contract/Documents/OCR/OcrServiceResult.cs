namespace Korkut.Contract.Documents.OCR;

/// <summary>
/// Text recovered from a scanned image / text-less PDF, plus the page count processed.
/// <paramref name="PageTexts"/> carries the per-page split so a caller can keep real page numbers
/// on the spans it builds (a citation has to say WHICH page of the scan a passage came from).
/// It is optional: an engine with no page concept leaves it unset and the joined
/// <paramref name="Text"/> is then the whole result.
/// </summary>
public sealed record OcrServiceResult(string Text, int Pages, IReadOnlyList<string>? PageTexts = null)
{
    /// <summary>The per-page texts, falling back to the joined text as a single page.</summary>
    public IReadOnlyList<string> PageTextsOrWhole => PageTexts is { Count: > 0 } pages ? pages : [Text];
}

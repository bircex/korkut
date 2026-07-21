namespace Korkut.Application.Documents;

/// <summary>Text recovered from a scanned image / text-less PDF, plus the page count processed.</summary>
public sealed record OcrResult(string Text, int Pages);

/// <summary>
/// Optical character recognition for documents that carry NO embedded text (scans, photographed
/// pages, image-only PDFs). Separate from <see cref="ITextExtractor"/> (which pulls already-present
/// text): OCR is heavy, rare, and native-dependency-bound (Tesseract), so it lives in its own host
/// (Korkut.Ocr) invoked on demand. The real engine is a deploy-time native binary; the in-code
/// default reports unavailable so the host still boots (503 on use).
/// </summary>
public interface IOcrEngine
{
    /// <summary>True when a real OCR backend is wired; false for the in-code scaffold.</summary>
    bool IsAvailable { get; }

    /// <summary>Recover text from the document bytes. Throws <see cref="OcrUnavailableException"/> if not wired.</summary>
    Task<OcrResult> ExtractAsync(Stream content, string contentType, CancellationToken ct = default);
}

/// <summary>Raised when OCR is requested but no engine is configured; surfaced as 503.</summary>
public sealed class OcrUnavailableException(string message) : Exception(message);

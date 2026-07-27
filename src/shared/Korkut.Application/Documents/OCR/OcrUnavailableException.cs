namespace Korkut.Application.Documents.OCR;

/// <summary>Raised when OCR is requested but no engine is configured; surfaced as 503.</summary>
public sealed class OcrUnavailableException(string message) : Exception(message);

namespace Korkut.Application.Documents.OCR;

/// <summary>Text recovered from a scanned image / text-less PDF, plus the page count processed.</summary>
public sealed record OcrServiceResult(string Text, int Pages);

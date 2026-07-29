using Korkut.Contract.Documents.OCR;

namespace Korkut.Infrastructure.Documents;

/// <summary>
/// The in-code OCR scaffold: reports unavailable and refuses every request (→ 503). Tesseract (or
/// another engine) is a native, deploy-time dependency; the real adapter replaces this registration
/// at deploy without any consumer change. Keeps the Korkut.Ocr host bootable and its contract
/// exercised in tests (which swap in a fake engine) before the binary exists.
/// </summary>
public sealed class DisabledOcrEngine : IOcrEngine
{
    private const string Error = "OCR motoru aktif değil!";
    
    public bool IsAvailable => false;

    public Task<OcrServiceResult> ExtractAsync(Stream content, string contentType, CancellationToken ct = default) =>
        throw new OcrUnavailableException(Error);
}

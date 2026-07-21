using Korkut.Application.Documents;

namespace Korkut.Infrastructure.Documents;

/// <summary>
/// The in-code OCR scaffold: reports unavailable and refuses every request (→ 503). Tesseract (or
/// another engine) is a native, deploy-time dependency; the real adapter replaces this registration
/// at deploy without any consumer change. Keeps the Korkut.Ocr host bootable and its contract
/// exercised in tests (which swap in a fake engine) before the binary exists.
/// </summary>
public sealed class DisabledOcrEngine : IOcrEngine
{
    public bool IsAvailable => false;

    public Task<OcrResult> ExtractAsync(Stream content, string contentType, CancellationToken ct = default) =>
        throw new OcrUnavailableException("OCR motoru yapılandırılmamış.");
}

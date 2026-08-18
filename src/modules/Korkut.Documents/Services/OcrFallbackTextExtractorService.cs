using Korkut.Contract.Documents;
using Korkut.Contract.Documents.OCR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Korkut.Documents.Services;

/// <summary>
/// Closes the gap a scanned document falls through: <see cref="PlainTextExtractorService"/> can only
/// read an EMBEDDED text layer, so an image-only PDF (a scanned dilekçe, tebligat or ihtarname — most
/// of what a lawyer actually uploads) yields no spans and the caller shows "belgeden metin
/// çıkarılamadı". This decorator runs the inner extractor first and, only when it comes back with no
/// usable text for a format OCR can handle, sends the same bytes to the OCR engine and builds the
/// spans from the recognised per-page text.
/// <para>
/// OCR is a best-effort ENRICHMENT: if the engine is unconfigured, unreachable or fails, the inner
/// extractor's (empty) result is returned unchanged so an upload never fails because OCR is down.
/// </para>
/// <para>
/// <see cref="ParserVersion"/> composes the inner version, so OCR-recovered text is a NEW immutable
/// artifact set (KDM-027) and never overwrites what the text-layer parser previously stored.
/// </para>
/// </summary>
public sealed partial class OcrFallbackTextExtractorService(
    ITextExtractorService inner,
    IOcrService ocrService,
    ILogger<OcrFallbackTextExtractorService>? logger = null) : ITextExtractorService
{
    // A scanned page is not always bit-empty: a stamp, a logo font or a header overlay can leave a
    // few stray glyphs behind. So "has a real text layer" is a threshold, not a non-empty check —
    // otherwise a scan with 3 junk characters would never reach OCR. Documents just under the
    // threshold are safe from a worse OCR result: OcrAsync keeps whichever text is longer.
    private const int MinTextLayerChars = 32;

    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();

    private readonly ILogger<OcrFallbackTextExtractorService> _logger =
        logger ?? NullLogger<OcrFallbackTextExtractorService>.Instance;

    public string ParserVersion => $"{inner.ParserVersion}+ocr-v1";

    public async Task<IReadOnlyList<TextExtractorServiceResult>> ExtractAsync(
        Stream content, string contentType, CancellationToken ct = default)
    {
        // Both the inner extractor and OCR read from the start, so the bytes must be re-readable.
        // A caller's stream is usually seekable (a buffered form file is); buffer when it is not.
        MemoryStream? buffered = null;
        var source = content;
        if (!content.CanSeek)
        {
            buffered = new MemoryStream();
            await content.CopyToAsync(buffered, ct);
            buffered.Position = 0;
            source = buffered;
        }

        try
        {
            var spans = await inner.ExtractAsync(source, contentType, ct);
            var textLayerLength = spans.Sum(s => s.Text.Length);
            if (textLayerLength >= MinTextLayerChars)
            {
                return spans;
            }

            if (!ocrService.IsAvailable || !await CanOcrAsync(source, contentType, ct))
            {
                return spans;
            }

            return await OcrAsync(source, contentType, spans, textLayerLength, ct);
        }
        finally
        {
            if (buffered is not null)
            {
                await buffered.DisposeAsync();
            }
        }
    }

    private async Task<IReadOnlyList<TextExtractorServiceResult>> OcrAsync(
        Stream source,
        string contentType,
        IReadOnlyList<TextExtractorServiceResult> fallback,
        int fallbackLength,
        CancellationToken ct)
    {
        try
        {
            source.Position = 0;
            var result = await ocrService.ExtractAsync(source, contentType, ct);
            var spans = PlainTextExtractorService.SpansFromPages([.. result.PageTextsOrWhole]);

            // Whichever source actually recovered more text wins. A genuinely short text-layer
            // document (a one-line receipt) trips the threshold above without being a scan, and its
            // embedded text is more faithful than any recognition of it — so OCR only replaces the
            // text layer when it beat it.
            if (spans.Sum(s => s.Text.Length) <= fallbackLength)
            {
                return fallback;
            }

            OcrRecovered(_logger, result.Pages, spans.Count);
            return spans;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never fail an upload because OCR is down — the caller already handles "no spans".
            OcrFailed(_logger, ex);
            return fallback;
        }
    }

    // Only formats an OCR engine can actually rasterise: PDFs and images. A DOCX/UDF/TXT with no
    // text is genuinely empty (or an unsupported container), and shipping it to OCR would burn
    // minutes of CPU to recover nothing.
    private static async Task<bool> CanOcrAsync(Stream source, string contentType, CancellationToken ct)
    {
        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
            || contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // The chat upload forwards the browser's declared type, which is often a useless
        // application/octet-stream — so sniff for a PDF the same way the inner extractor does.
        source.Position = 0;
        var head = new byte[PdfMagic.Length];
        var filled = 0;
        while (filled < head.Length)
        {
            var read = await source.ReadAsync(head.AsMemory(filled), ct);
            if (read == 0)
            {
                break;
            }

            filled += read;
        }

        return filled == head.Length && head.AsSpan().SequenceEqual(PdfMagic);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Document had no text layer; OCR recovered text from {Pages} page(s) as {Spans} span(s).")]
    private static partial void OcrRecovered(ILogger logger, int pages, int spans);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "OCR fallback failed; keeping the empty text-layer result.")]
    private static partial void OcrFailed(ILogger logger, Exception ex);
}

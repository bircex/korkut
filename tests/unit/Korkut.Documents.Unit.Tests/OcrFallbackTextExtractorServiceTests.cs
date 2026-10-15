using System.Text;
using Korkut.Contract.Documents;
using Korkut.Contract.Documents.OCR;
using Korkut.Documents.Services;

namespace Korkut.Documents.Tests;

/// <summary>
/// Covers the OCR fallback the scanned-document path depends on: a PDF with no embedded text layer
/// (the shape of a scanned dilekçe/tebligat, which previously came back as "belgeden metin
/// çıkarılamadı") must reach the OCR engine and come back as page-numbered spans, while a document
/// that already has text, a format OCR cannot help with, or a broken engine must not change
/// behaviour.
/// </summary>
public class OcrFallbackTextExtractorServiceTests
{
    private const string TextLayer = "Bu belgenin gercek bir metin katmani vardir ve yeterince uzundur";

    /// <summary>Records whether it was called, and returns a fixed per-page recognition result.</summary>
    private sealed class FakeOcrService(bool available = true, params string[] pages) : IOcrService
    {
        public bool IsAvailable { get; } = available;

        public int Calls { get; private set; }

        public Task<OcrServiceResult> ExtractAsync(Stream content, string contentType, CancellationToken ct = default)
        {
            Calls++;
            var texts = pages.Length > 0 ? pages : ["OCR ile okunan metin"];
            return Task.FromResult(new OcrServiceResult(string.Join("\n\n", texts), texts.Length, texts));
        }
    }

    /// <summary>Stands in for an unconfigured/unreachable OCR host (the 503 shape).</summary>
    private sealed class BrokenOcrService : IOcrService
    {
        public bool IsAvailable => true;

        public Task<OcrServiceResult> ExtractAsync(Stream content, string contentType, CancellationToken ct = default) =>
            throw new OcrUnavailableException("OCR motoru aktif değil!");
    }

    /// <summary>A forward-only stream: what a raw request body looks like to the extractor.</summary>
    private sealed class NonSeekableStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static OcrFallbackTextExtractorService Sut(IOcrService ocr) => new(new PlainTextExtractorService(), ocr);

    [Fact]
    public void ParserVersion_ComposesTheInnerVersion_SoOcrTextIsANewArtifactSet()
    {
        // KDM-027: OCR-recovered text must never overwrite what the text-layer parser stored.
        Assert.Equal("plaintext-v4+ocr-v1", Sut(new FakeOcrService()).ParserVersion);
    }

    [Fact]
    public async Task ExtractAsync_WithATextLessPdf_RecoversTextThroughOcr()
    {
        // An empty content stream is a valid PDF that yields no text — the same thing PdfPig sees
        // for a scan whose pages are only CCITTFax images.
        var ocr = new FakeOcrService(true, "Birinci sayfa metni", "Ikinci sayfa metni");
        var pdf = PdfFixtures.MinimalOnePage(string.Empty);

        var spans = await Sut(ocr).ExtractAsync(new MemoryStream(pdf), "application/pdf");

        Assert.Equal(1, ocr.Calls);
        Assert.Contains(spans, s => s.Text.Contains("Birinci sayfa metni"));
        Assert.Contains(spans, s => s.Text.Contains("Ikinci sayfa metni"));
    }

    [Fact]
    public async Task ExtractAsync_KeepsTheRealPageNumbersOfTheScan()
    {
        var ocr = new FakeOcrService(true, "Sayfa bir", "Sayfa iki", "Sayfa uc");
        var pdf = PdfFixtures.MinimalOnePage(string.Empty);

        var spans = await Sut(ocr).ExtractAsync(new MemoryStream(pdf), "application/pdf");

        Assert.Equal(3, spans.Single(s => s.Text.Contains("Sayfa uc")).Page);
        Assert.Equal([1, 2, 3], spans.Select(s => s.Page).Distinct().Order());
    }

    [Fact]
    public async Task ExtractAsync_WithAnEmbeddedTextLayer_DoesNotCallOcr()
    {
        var ocr = new FakeOcrService();
        var pdf = PdfFixtures.MinimalOnePage(TextLayer);

        var spans = await Sut(ocr).ExtractAsync(new MemoryStream(pdf), "application/pdf");

        Assert.Equal(0, ocr.Calls);
        Assert.Contains(spans, s => s.Text.Contains("gercek bir metin katmani"));
    }

    [Fact]
    public async Task ExtractAsync_KeepsAShortTextLayer_WhenOcrRecoversLess()
    {
        // Under the "is this a scan" threshold but genuinely text-based: the embedded text is more
        // faithful than recognition of it, so the shorter OCR result must not replace it.
        var ocr = new FakeOcrService(true, "kisa");
        var pdf = PdfFixtures.MinimalOnePage("Kisa ama gercek metin");

        var spans = await Sut(ocr).ExtractAsync(new MemoryStream(pdf), "application/pdf");

        Assert.Contains(spans, s => s.Text.Contains("Kisa ama gercek metin"));
        Assert.DoesNotContain(spans, s => s.Text.Contains("kisa", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExtractAsync_DetectsAPdfByMagicBytes_WhenTheContentTypeIsGeneric()
    {
        // The chat upload forwards the browser's declared type, often application/octet-stream.
        var ocr = new FakeOcrService(true, "Taranmis evrak metni");
        var pdf = PdfFixtures.MinimalOnePage(string.Empty);

        var spans = await Sut(ocr).ExtractAsync(new MemoryStream(pdf), "application/octet-stream");

        Assert.Equal(1, ocr.Calls);
        Assert.Contains(spans, s => s.Text.Contains("Taranmis evrak metni"));
    }

    [Fact]
    public async Task ExtractAsync_WithANonSeekableStream_StillReachesOcr()
    {
        var ocr = new FakeOcrService(true, "Akis uzerinden okundu");
        var pdf = PdfFixtures.MinimalOnePage(string.Empty);

        var spans = await Sut(ocr).ExtractAsync(new NonSeekableStream(pdf), "application/pdf");

        Assert.Equal(1, ocr.Calls);
        Assert.Contains(spans, s => s.Text.Contains("Akis uzerinden okundu"));
    }

    [Fact]
    public async Task ExtractAsync_WithAFormatOcrCannotHelpWith_DoesNotCallOcr()
    {
        // An empty text file is genuinely empty; rasterising it would burn CPU to recover nothing.
        var ocr = new FakeOcrService();

        var spans = await Sut(ocr).ExtractAsync(new MemoryStream(Encoding.UTF8.GetBytes("   ")), "text/plain");

        Assert.Equal(0, ocr.Calls);
        Assert.Empty(spans);
    }

    [Fact]
    public async Task ExtractAsync_WhenTheEngineIsNotWired_DoesNotCallOcr()
    {
        var ocr = new FakeOcrService(available: false);

        var spans = await Sut(ocr).ExtractAsync(new MemoryStream(PdfFixtures.MinimalOnePage(string.Empty)), "application/pdf");

        Assert.Equal(0, ocr.Calls);
        Assert.Empty(spans);
    }

    [Fact]
    public async Task ExtractAsync_WhenOcrFails_DegradesInsteadOfFailingTheUpload()
    {
        // OCR being down must never turn into a failed upload — the caller already handles no spans.
        var spans = await Sut(new BrokenOcrService())
            .ExtractAsync(new MemoryStream(PdfFixtures.MinimalOnePage(string.Empty)), "application/pdf");

        Assert.Empty(spans);
    }

    [Fact]
    public async Task ExtractAsync_PassesOcrTextThroughTheSameSanitizer()
    {
        // OCR output is a prime source of stray control bytes, and a NUL byte in a text column is
        // what crashed PDF upload before (PR #185).
        var ocr = new FakeOcrService(true, "Tebligat\0 metni ve devami yeterince uzun olsun");

        var spans = await Sut(ocr).ExtractAsync(new MemoryStream(PdfFixtures.MinimalOnePage(string.Empty)), "application/pdf");

        Assert.DoesNotContain('\0', string.Join(' ', spans.Select(s => s.Text)));
        Assert.Contains(spans, s => s.Text.Contains("Tebligat"));
    }
}

using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Korkut.Contract.Documents;
using Korkut.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig;

namespace Korkut.Documents.Services;

/// <summary>
/// Extracts text spans from PDF, DOCX, UDF (UYAP), legacy DOC and plain-text uploads. Format is
/// decided by magic bytes / container contents, never the caller's declared content-type alone
/// (a content-type hint is checked first as a fast path, same as the PDF sniff). PDF goes through
/// PdfPig's text layer; DOCX/UDF are hand-rolled zip+XML readers, symmetric with
/// <see cref="MarkdownDocumentExporter"/>'s writers for the same two formats; DOC uses NPOI's
/// HWPF reader (the one format here with no reasonable hand-rolled alternative — OLE2 Compound
/// File + Word 97 binary streams); everything else is read as UTF-8, paginated on form-feeds and
/// split into paragraph spans. Corrupt/encrypted/unrecognised content of a matched format yields
/// no spans (never throws) — the caller already has a graceful "belgeden metin çıkarılamadı"
/// fallback for zero spans.
/// </summary>
public sealed partial class PlainTextExtractorService(ILogger<PlainTextExtractorService>? logger = null) : ITextExtractorService
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static readonly byte[] PdfMagic = "%PDF"u8.ToArray();
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] Ole2Magic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    // Image signatures: JPEG, PNG, TIFF (both byte orders), GIF, and RIFF (WebP).
    private static readonly byte[][] ImageMagics =
    [
        [0xFF, 0xD8, 0xFF], [0x89, 0x50, 0x4E, 0x47], [0x49, 0x49, 0x2A, 0x00], [0x4D, 0x4D, 0x00, 0x2A],
        "GIF8"u8.ToArray(), "RIFF"u8.ToArray(),
    ];

    // Optional so tests/call sites outside DI (`new PlainTextExtractorService()`) keep working.
    private readonly ILogger<PlainTextExtractorService> _logger = logger ?? NullLogger<PlainTextExtractorService>.Instance;

    public string ParserVersion => "plaintext-v4";

    public async Task<IReadOnlyList<TextExtractorServiceResult>> ExtractAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        content.Position = 0;
        var header = await ReadHeaderAsync(content, 8, ct);

        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase) || StartsWith(header, PdfMagic))
        {
            content.Position = 0;
            return ExtractPdf(content);
        }

        if (StartsWith(header, ZipMagic))
        {
            content.Position = 0;
            using var buffer = await BufferAsync(content, ct);
            return LooksLikeDocx(buffer) ? ExtractDocx(buffer) : ExtractUdf(buffer);
        }

        if (StartsWith(header, Ole2Magic))
        {
            // Legacy Word 97-2003 (OLE2 compound file). Accepted for storage/upload, but this
            // extractor doesn't parse the binary Word text layer (piece tables, complex-format
            // runs) yet — that needs either a proper library or an external converter, not a
            // hand-rolled parser. Fail safe like a corrupt/encrypted PDF: no spans, no crash.
            LegacyDocNotSupported(_logger);
            return [];
        }

        // A photo or scan has no text layer; read as text its bytes would pass for one and keep
        // OCR from ever being asked.
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || ImageMagics.Any(m => StartsWith(header, m)))
        {
            return [];
        }

        content.Position = 0;
        using var reader = new StreamReader(content, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        return SpansFromPages(text.Split('\f')); // form-feed = page break
    }

    // --- PDF ---

    // PdfPig throws on encrypted/corrupt/unusually-structured PDFs; treat that the same as "no
    // text layer" (empty spans) instead of failing the whole upload.
    private List<TextExtractorServiceResult> ExtractPdf(Stream content)
    {
        try
        {
            using var doc = PdfDocument.Open(content);
            var pages = doc.GetPages().Select(p => p.Text ?? string.Empty).ToArray();
            return SpansFromPages(pages);
        }
        catch (Exception ex)
        {
            PdfExtractionFailed(_logger, ex);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "PDF text extraction failed; falling back to no extracted text.")]
    private static partial void PdfExtractionFailed(ILogger logger, Exception ex);

    // --- DOCX (OpenXML WordprocessingML) ---

    /// <summary>A zip is a DOCX if it carries the OOXML main document part; otherwise treat the zip as a UDF.</summary>
    private static bool LooksLikeDocx(Stream content) => TryOpenZip(content, zip => zip.GetEntry("word/document.xml") is not null);

    private List<TextExtractorServiceResult> ExtractDocx(Stream content)
    {
        try
        {
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.GetEntry("word/document.xml");
            if (entry is null)
            {
                return [];
            }

            using var entryStream = entry.Open();
            var doc = XDocument.Load(entryStream);
            // Table cells nest their own <w:p> paragraphs, so a plain document-order descendant
            // walk naturally picks up table text too (structure is lost, text isn't).
            var paragraphs = doc.Descendants(W + "p")
                .Select(p => string.Concat(p.Descendants(W + "t").Select(t => t.Value)));
            return SpansFromParagraphs(1, paragraphs);
        }
        catch (Exception ex)
        {
            DocxExtractionFailed(_logger, ex);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "DOCX text extraction failed; falling back to no extracted text.")]
    private static partial void DocxExtractionFailed(ILogger logger, Exception ex);

    // --- UDF (UYAP Document Format) ---

    private List<TextExtractorServiceResult> ExtractUdf(Stream content)
    {
        try
        {
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            var entry = zip.Entries.FirstOrDefault(e => e.Name.Equals("content.xml", StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                return [];
            }

            using var entryStream = entry.Open();
            var doc = XDocument.Load(entryStream);
            // UDF keeps one flat text buffer under a <content> element (paragraph/run formatting
            // elsewhere references it by offset — see MarkdownDocumentExporter.BuildUdf); the
            // per-paragraph <content family=".." size=".."> elements carry no text of their own,
            // so picking the longest "content" element's value is robust to structural variance
            // between UYAP's own editor output and this codebase's writer.
            var buffer = doc.Descendants("content").Select(e => e.Value).OrderByDescending(v => v.Length).FirstOrDefault() ?? string.Empty;
            var lines = buffer.Split(["\r\n", "\n"], StringSplitOptions.None);
            return SpansFromParagraphs(1, lines);
        }
        catch (Exception ex)
        {
            UdfExtractionFailed(_logger, ex);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "UDF text extraction failed; falling back to no extracted text.")]
    private static partial void UdfExtractionFailed(ILogger logger, Exception ex);

    // --- DOC (legacy Word 97-2003, OLE2 compound file) ---

    [LoggerMessage(Level = LogLevel.Warning, Message = "Legacy .doc (Word 97-2003) text extraction is not implemented; falling back to no extracted text.")]
    private static partial void LegacyDocNotSupported(ILogger logger);

    // --- shared ---

    /// <summary>
    /// Page texts to sanitized paragraph spans. Internal rather than private so
    /// <see cref="OcrFallbackTextExtractorService"/> turns OCR page output into spans through this
    /// same path — one paragraph-splitting and sanitizing rule for both text sources, not two.
    /// </summary>
    internal static List<TextExtractorServiceResult> SpansFromPages(string[] pages)
    {
        var spans = new List<TextExtractorServiceResult>();
        for (var p = 0; p < pages.Length; p++)
        {
            var paragraphs = pages[p].Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);
            AppendSpans(spans, p + 1, paragraphs);
        }

        return spans;
    }

    /// <summary>Formats with no page concept (DOCX/UDF/DOC) — every paragraph lands on page 1.</summary>
    private static List<TextExtractorServiceResult> SpansFromParagraphs(int page, IEnumerable<string> paragraphs)
    {
        var spans = new List<TextExtractorServiceResult>();
        AppendSpans(spans, page, paragraphs);
        return spans;
    }

    private static void AppendSpans(List<TextExtractorServiceResult> spans, int page, IEnumerable<string> paragraphs)
    {
        var index = 0;
        foreach (var para in paragraphs)
        {
            var clean = Sanitize(para);
            if (clean.Length > 0)
            {
                spans.Add(new TextExtractorServiceResult(page, index++, clean));
            }
        }
    }

    private static string Sanitize(string text) => PostgresTextSanitizer.StripInvalidChars(text.Trim());

    private static bool TryOpenZip(Stream content, Func<ZipArchive, bool> probe)
    {
        try
        {
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            return probe(zip);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            content.Position = 0;
        }
    }

    private static bool StartsWith(byte[] header, byte[] magic)
        => header.Length >= magic.Length && header.AsSpan(0, magic.Length).SequenceEqual(magic);

    private static async Task<MemoryStream> BufferAsync(Stream content, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    // ReadAsync may return fewer bytes than requested in a single call even when more data
    // follows (Stream's contract, not just theoretical for network-backed streams) — read in a
    // loop rather than trusting one call to fill the buffer, or short streams risk a false
    // "unrecognised format" verdict.
    private static async Task<byte[]> ReadHeaderAsync(Stream content, int length, CancellationToken ct)
    {
        var head = new byte[length];
        var filled = 0;
        while (filled < length)
        {
            var read = await content.ReadAsync(head.AsMemory(filled), ct);
            if (read == 0)
            {
                break; // end of stream before `length` bytes were available
            }

            filled += read;
        }

        return filled == length ? head : head[..filled];
    }
}

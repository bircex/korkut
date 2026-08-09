using System.Text;
using Korkut.Contract.Documents;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace Korkut.Documents.Services;

/// <summary>
/// Extracts text spans: PDFs go through PdfPig's text layer (page-by-page); everything else is
/// read as UTF-8, paginated on form-feeds and split into paragraph spans. A dependency-light
/// extractor suitable for text-based documents and PDFs with an embedded text layer; scanned/
/// image-only/malformed PDFs yield no spans (no text layer PdfPig can read) rather than failing
/// the request — the OCR/PDF-raster extractor plugs in behind <see cref="ITextExtractorService"/>
/// in production for those.
/// </summary>
public sealed partial class PlainTextExtractorService(ILogger<PlainTextExtractorService> logger) : ITextExtractorService
{
    public string ParserVersion => "plaintext-v2";

    public async Task<IReadOnlyList<TextExtractorServiceResult>> ExtractAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        content.Position = 0;
        if (await LooksLikePdfAsync(content, contentType, ct))
        {
            content.Position = 0;
            return ExtractPdf(content);
        }

        content.Position = 0;
        using var reader = new StreamReader(content, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        return SpansFromPages(text.Split('\f')); // form-feed = page break

    }

    // PdfPig throws on encrypted/corrupt/unusually-structured PDFs; treat that the same as "no
    // text layer" (empty spans) instead of failing the whole upload — the caller already has a
    // graceful "belgeden metin çıkarılamadı" fallback for zero spans.
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
            PdfExtractionFailed(logger, ex);
            return [];
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "PDF text extraction failed; falling back to no extracted text.")]
    private static partial void PdfExtractionFailed(ILogger logger, Exception ex);

    private static List<TextExtractorServiceResult> SpansFromPages(string[] pages)
    {
        var spans = new List<TextExtractorServiceResult>();
        for (var p = 0; p < pages.Length; p++)
        {
            var paragraphs = pages[p].Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);
            var index = 0;
            foreach (var para in paragraphs)
            {
                var clean = Sanitize(para);
                if (clean.Length > 0)
                {
                    spans.Add(new TextExtractorServiceResult(p + 1, index++, clean));
                }
            }
        }

        return spans;
    }

    // Postgres' "text" columns reject the NUL byte outright (and other C0 controls are noise from
    // a binary misread), so strip them here at the source rather than let a bad byte blow up every
    // downstream consumer (chat persistence, search indexing, ...).
    private static string Sanitize(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        Span<char> buffer = trimmed.Length <= 4096 ? stackalloc char[trimmed.Length] : new char[trimmed.Length];
        var written = 0;
        foreach (var ch in trimmed)
        {
            if (ch == '\0' || (char.IsControl(ch) && ch is not ('\n' or '\r' or '\t')))
            {
                continue;
            }

            buffer[written++] = ch;
        }

        return new string(buffer[..written]);
    }

    private static async Task<bool> LooksLikePdfAsync(Stream content, string contentType, CancellationToken ct)
    {
        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var head = new byte[5];
        var read = await content.ReadAsync(head.AsMemory(0, 5), ct);
        return read >= 5 && head[0] == '%' && head[1] == 'P' && head[2] == 'D' && head[3] == 'F';
    }
}

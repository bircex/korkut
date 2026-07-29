using System.Text;
using Korkut.Contract.Documents;

namespace Korkut.Documents.Services;

/// <summary>
/// Extracts UTF-8 text, paginated on form-feeds and split into paragraph spans. A
/// deterministic, dependency-free extractor suitable for text-based documents and
/// CI; the OCR/PDF extractors plug in behind <see cref="ITextExtractorService"/> in
/// production with their own parser versions.
/// </summary>
public sealed class PlainTextExtractorService : ITextExtractorService
{
    public string ParserVersion => "plaintext-v1";

    public async Task<IReadOnlyList<TextExtractorServiceResult>> ExtractAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        content.Position = 0;
        using var reader = new StreamReader(content, Encoding.UTF8, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);

        var spans = new List<TextExtractorServiceResult>();
        var pages = text.Split('\f'); // form-feed = page break
        for (var p = 0; p < pages.Length; p++)
        {
            var paragraphs = pages[p].Split(["\n\n", "\r\n\r\n"], StringSplitOptions.RemoveEmptyEntries);
            var index = 0;
            foreach (var para in paragraphs)
            {
                var trimmed = para.Trim();
                if (trimmed.Length > 0)
                {
                    spans.Add(new TextExtractorServiceResult(p + 1, index++, trimmed));
                }
            }
        }

        return spans;
    }
}

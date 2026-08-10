using System.IO.Compression;
using System.Text;
using Korkut.Documents.Services;

namespace Korkut.Documents.Tests;

/// <summary>
/// DOCX/UDF/DOC/TXT upload support: uploads used to be rejected for everything but PDF/DOCX at
/// the validator gate (UploadValidatorServiceTests), and DOCX itself had no text extraction at
/// all. DOCX/UDF fixtures here are built with the SAME production code that writes them
/// (MarkdownDocumentExporter) — a true round-trip check, not a hand-guessed byte layout.
/// </summary>
public class MultiFormatExtractionTests
{
    private static readonly PlainTextExtractorService Sut = new();

    // --- DOCX ---

    [Fact]
    public async Task ExtractAsync_Docx_ExtractsHeadingAndParagraphText()
    {
        var docx = MarkdownDocumentExporter.Export("docx", "Dilekce", "# Basvuru\n\nSayin Mahkeme Baskanligina.");

        var spans = await Sut.ExtractAsync(new MemoryStream(docx.Bytes), "application/docx");

        var joined = string.Join(' ', spans.Select(s => s.Text));
        Assert.Contains("Basvuru", joined);
        Assert.Contains("Sayin Mahkeme Baskanligina", joined);
        Assert.All(spans, s => Assert.Equal(1, s.Page)); // OOXML has no page markers in the XML
    }

    [Fact]
    public async Task ExtractAsync_Docx_ExtractsTableCellText()
    {
        var docx = MarkdownDocumentExporter.Export("docx", "Tablo", "| Ad | Tutar |\n| --- | --- |\n| Vekalet | 5000 |");

        var spans = await Sut.ExtractAsync(new MemoryStream(docx.Bytes), "application/docx");

        var joined = string.Join(' ', spans.Select(s => s.Text));
        Assert.Contains("Vekalet", joined);
        Assert.Contains("5000", joined);
    }

    [Fact]
    public async Task ExtractAsync_DetectsDocxByMagicBytes_EvenWithGenericContentType()
    {
        var docx = MarkdownDocumentExporter.Export("docx", "Belge", "Merhaba dunya");

        var spans = await Sut.ExtractAsync(new MemoryStream(docx.Bytes), "application/octet-stream");

        Assert.Contains(spans, s => s.Text.Contains("Merhaba"));
    }

    [Fact]
    public async Task ExtractAsync_DocxWithoutDocumentXml_ReturnsEmptySpansInsteadOfThrowing()
    {
        // A zip with the right magic bytes but no word/document.xml is not a real DOCX (nor a
        // UDF, since it also has no content.xml) — must degrade gracefully either way.
        var zip = BuildZip(("readme.txt", "not a document"));

        var spans = await Sut.ExtractAsync(new MemoryStream(zip), "application/docx");

        Assert.Empty(spans);
    }

    [Fact]
    public async Task ExtractAsync_DocxWithMalformedDocumentXml_ReturnsEmptySpansInsteadOfThrowing()
    {
        var zip = BuildZip(("word/document.xml", "<not-well-formed"));

        var spans = await Sut.ExtractAsync(new MemoryStream(zip), "application/docx");

        Assert.Empty(spans);
    }

    // --- UDF (UYAP) ---

    [Fact]
    public async Task ExtractAsync_Udf_ExtractsTextBuffer()
    {
        var udf = MarkdownDocumentExporter.Export("udf", "Vekaletname", "# Madde 1\n\nIsbu vekaletname ile.");

        var spans = await Sut.ExtractAsync(new MemoryStream(udf.Bytes), "application/udf");

        var joined = string.Join(' ', spans.Select(s => s.Text));
        Assert.Contains("Madde 1", joined);
        Assert.Contains("Isbu vekaletname ile", joined);
    }

    [Fact]
    public async Task ExtractAsync_DetectsUdfByMagicBytes_EvenWithGenericContentType()
    {
        var udf = MarkdownDocumentExporter.Export("udf", "Belge", "UYAP metni");

        var spans = await Sut.ExtractAsync(new MemoryStream(udf.Bytes), "application/octet-stream");

        Assert.Contains(spans, s => s.Text.Contains("UYAP"));
    }

    [Fact]
    public async Task ExtractAsync_UdfIsNeverConfusedWithDocx_EvenThoughBothAreZips()
    {
        var udf = MarkdownDocumentExporter.Export("udf", "Belge", "sadece udf icerigi");

        var spans = await Sut.ExtractAsync(new MemoryStream(udf.Bytes), "application/udf");

        // If this were (mis)routed through the DOCX reader, word/document.xml wouldn't exist and
        // spans would come back empty instead of containing the real text.
        Assert.Contains(spans, s => s.Text.Contains("sadece udf icerigi"));
    }

    [Fact]
    public async Task ExtractAsync_CorruptUdf_ReturnsEmptySpansInsteadOfThrowing()
    {
        var zip = BuildZip(("content.xml", "<not-well-formed"));

        var spans = await Sut.ExtractAsync(new MemoryStream(zip), "application/udf");

        Assert.Empty(spans);
    }

    [Fact]
    public async Task ExtractAsync_UdfTextIsSanitized_LikeEveryOtherFormat()
    {
        var udf = MarkdownDocumentExporter.Export("udf", "Belge", "once\0sonra");

        var spans = await Sut.ExtractAsync(new MemoryStream(udf.Bytes), "application/udf");

        Assert.All(spans, s => Assert.DoesNotContain('\0', s.Text));
    }

    // --- DOC (legacy Word 97-2003) ---

    [Fact]
    public async Task ExtractAsync_LegacyDoc_IsAcceptedAndReturnsEmptySpansWithoutThrowing()
    {
        // OLE2 magic bytes only — real Word-97 stream parsing isn't implemented (see
        // PlainTextExtractorService's LegacyDocNotSupported). The contract under test is
        // "accepted, never crashes", matching the corrupt-PDF/corrupt-UDF/corrupt-DOCX fallback.
        var ole2 = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0 };

        var spans = await Sut.ExtractAsync(new MemoryStream(ole2), "application/doc");

        Assert.Empty(spans);
    }

    // --- helpers ---

    private static byte[] BuildZip(params (string Name, string Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = zip.CreateEntry(name);
                using var s = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                s.Write(bytes, 0, bytes.Length);
            }
        }

        return ms.ToArray();
    }
}

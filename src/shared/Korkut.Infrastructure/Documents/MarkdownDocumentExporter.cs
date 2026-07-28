using Korkut.Contract.Export;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Korkut.Application.Export;

namespace Korkut.Infrastructure.Documents;

/// <summary>
/// Renders a markdown document to a downloadable Word (.docx, OpenXML WordprocessingML)
/// or UYAP (.udf) file. Both formats are ZIP+XML and are built by hand (no third-party
/// dependency): docx uses direct run formatting (headings, bold, tables, bullets); UDF
/// produces the offset-based content/elements structure UYAP expects.
/// </summary>
public static class MarkdownDocumentExporter
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";

    public static ExportedDocument Export(string format, string title, string markdown)
    {
        var blocks = MarkdownBlock.Parse(markdown);
        var safeName = Slug(title);
        return format switch
        {
            "docx" => new ExportedDocument(BuildDocx(blocks), "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"{safeName}.docx"),
            "udf" => new ExportedDocument(BuildUdf(blocks), "application/octet-stream", $"{safeName}.udf"),
            _ => throw new ArgumentException($"Unsupported export format '{format}'.", nameof(format)),
        };
    }

    // --- DOCX (OpenXML WordprocessingML) ---

    private static byte[] BuildDocx(IReadOnlyList<MarkdownBlock> blocks)
    {
        var body = new XElement(W + "body");
        foreach (var block in blocks)
        {
            switch (block.Kind)
            {
                case BlockKind.Heading:
                    body.Add(Paragraph(block.Text, bold: true, sizeHalfPoints: block.Level == 1 ? 32 : block.Level == 2 ? 28 : 24));
                    break;
                case BlockKind.Bullet:
                    body.Add(Paragraph("• " + block.Text));
                    break;
                case BlockKind.Table:
                    body.Add(Table(block.Rows!));
                    break;
                default:
                    body.Add(Paragraph(block.Text));
                    break;
            }
        }

        body.Add(new XElement(W + "sectPr",
            new XElement(W + "pgSz", new XAttribute(W + "w", 11906), new XAttribute(W + "h", 16838))));

        var document = new XDocument(new XElement(W + "document", new XAttribute(XNamespace.Xmlns + "w", W.NamespaceName), body));

        var contentTypes = new XDocument(new XElement(Ct + "Types",
            TypeDefault("rels", "application/vnd.openxmlformats-package.relationships+xml"),
            TypeDefault("xml", "application/xml"),
            new XElement(Ct + "Override", new XAttribute("PartName", "/word/document.xml"),
                new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"))));

        var rels = new XDocument(new XElement(Rel + "Relationships",
            new XElement(Rel + "Relationship",
                new XAttribute("Id", "rId1"),
                new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                new XAttribute("Target", "word/document.xml"))));

        return Zip(new (string, XDocument)[]
        {
            ("[Content_Types].xml", contentTypes),
            ("_rels/.rels", rels),
            ("word/document.xml", document),
        });

        XElement TypeDefault(string ext, string ctype) =>
            new(Ct + "Default", new XAttribute("Extension", ext), new XAttribute("ContentType", ctype));
    }

    private static XElement Paragraph(string text, bool bold = false, int? sizeHalfPoints = null)
    {
        var p = new XElement(W + "p");
        foreach (var run in InlineRun.Split(text))
        {
            var rPr = new XElement(W + "rPr");
            if (bold || run.Bold)
            {
                rPr.Add(new XElement(W + "b"));
            }

            if (sizeHalfPoints is int sz)
            {
                rPr.Add(new XElement(W + "sz", new XAttribute(W + "val", sz)));
            }

            var r = new XElement(W + "r");
            if (rPr.HasElements)
            {
                r.Add(rPr);
            }

            r.Add(new XElement(W + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), run.Text));
            p.Add(r);
        }

        return p;
    }

    private static XElement Table(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var tbl = new XElement(W + "tbl",
            new XElement(W + "tblPr",
                new XElement(W + "tblBorders",
                    Border("top"), Border("left"), Border("bottom"), Border("right"),
                    Border("insideH"), Border("insideV"))));
        foreach (var row in rows)
        {
            var tr = new XElement(W + "tr");
            foreach (var cell in row)
            {
                tr.Add(new XElement(W + "tc",
                    new XElement(W + "tcPr", new XElement(W + "tcW", new XAttribute(W + "w", 0), new XAttribute(W + "type", "auto"))),
                    Paragraph(cell)));
            }

            tbl.Add(tr);
        }

        return tbl;

        XElement Border(string side) => new(W + side,
            new XAttribute(W + "val", "single"), new XAttribute(W + "sz", 4), new XAttribute(W + "space", 0), new XAttribute(W + "color", "auto"));
    }

    // --- UDF (UYAP Document Format) ---

    private static byte[] BuildUdf(IReadOnlyList<MarkdownBlock> blocks)
    {
        // UDF references paragraph runs by offset into one flat text buffer.
        var text = new StringBuilder();
        var elements = new XElement("elements",
            new XAttribute("resolution", "72"),
            new XAttribute("hierarchy", "0"),
            new XAttribute("pageFormat", "1"));

        foreach (var block in blocks)
        {
            var line = block.Kind == BlockKind.Table
                ? string.Join("\n", block.Rows!.Select(r => string.Join("\t", r)))
                : block.Kind == BlockKind.Bullet ? "• " + block.Text : block.Text;
            var start = text.Length;
            text.Append(line).Append('\n');
            elements.Add(new XElement("paragraph",
                new XAttribute("Alignment", "0"),
                new XElement("content",
                    new XAttribute("family", "Dialog"),
                    new XAttribute("size", block.Kind == BlockKind.Heading ? 14 : 12),
                    new XAttribute("bold", block.Kind == BlockKind.Heading ? "true" : "false"),
                    new XAttribute("startOffset", start),
                    new XAttribute("length", text.Length - start))));
        }

        var template = new XElement("template",
            new XAttribute("format_id", "1.0"),
            new XElement("content", new XCData(text.ToString())),
            elements);
        var contentXml = new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), template);

        // A .udf is a zip whose single entry is content.xml.
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("content.xml", CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            contentXml.Save(writer);
        }

        return ms.ToArray();
    }

    // --- shared ---

    private static byte[] Zip((string Path, XDocument Doc)[] parts)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, doc) in parts)
            {
                var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
                using var stream = entry.Open();
                using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                doc.Save(writer);
            }
        }

        return ms.ToArray();
    }

    private static string Slug(string title)
    {
        var cleaned = new string((title ?? "belge").Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
        return string.IsNullOrWhiteSpace(cleaned) ? "belge" : cleaned.Length > 80 ? cleaned[..80] : cleaned;
    }
}

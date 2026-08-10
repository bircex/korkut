using System.Text;

namespace Korkut.Documents.Tests;

/// <summary>
/// Hand-built, byte-exact minimal PDFs (correct xref offsets, no external PDF library) for
/// exercising the real PdfPig extraction path in tests without shipping binary fixture files.
/// Text must be ASCII — no escaping of PDF string-literal special characters is implemented.
/// </summary>
internal static class PdfFixtures
{
    public static byte[] MinimalOnePage(string text) => Build([BuildPageContent(text)]);

    public static byte[] TwoPages(string page1Text, string page2Text) => Build([BuildPageContent(page1Text), BuildPageContent(page2Text)]);

    private static string BuildPageContent(string text)
    {
        var content = $"BT /F1 24 Tf 20 100 Td ({text}) Tj ET";
        return $"<< /Length {Encoding.ASCII.GetByteCount(content)} >>\nstream\n{content}\nendstream";
    }

    /// <summary>
    /// Assembles a well-formed single-xref-section PDF: 1 Catalog, 1 Pages, N Page objects (each
    /// its own content stream + shared Helvetica font), with byte-accurate offsets computed as we
    /// write — PDF's xref table requires exact byte positions, so offsets are recorded from the
    /// actual stream position rather than hand-calculated.
    /// </summary>
    private static byte[] Build(IReadOnlyList<string> pageContents)
    {
        var pageCount = pageContents.Count;
        var pageObjNums = Enumerable.Range(0, pageCount).Select(i => 3 + i).ToArray(); // 3..3+n-1
        var fontObjNum = 3 + pageCount;
        var contentObjNums = Enumerable.Range(0, pageCount).Select(i => fontObjNum + 1 + i).ToArray();

        var kids = string.Join(' ', pageObjNums.Select(n => $"{n} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>", // 1
            $"<< /Type /Pages /Kids [{kids}] /Count {pageCount} >>", // 2
        };
        for (var i = 0; i < pageCount; i++)
        {
            objects.Add(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] " +
                $"/Resources << /Font << /F1 {fontObjNum} 0 R >> >> /Contents {contentObjNums[i]} 0 R >>"); // page i
        }

        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>"); // font
        objects.AddRange(pageContents); // content streams

        using var ms = new MemoryStream();
        void Write(string s) => ms.Write(Encoding.ASCII.GetBytes(s));

        Write("%PDF-1.4\n");
        var offsets = new long[objects.Count + 1]; // 1-indexed, offsets[0] unused (free entry)
        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i + 1] = ms.Position;
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefOffset = ms.Position;
        Write($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        for (var i = 1; i <= objects.Count; i++)
        {
            Write($"{offsets[i]:D10} 00000 n \n");
        }

        Write($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");
        return ms.ToArray();
    }
}

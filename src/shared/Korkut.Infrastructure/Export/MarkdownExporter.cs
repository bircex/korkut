using Korkut.Contract.Export;
using Korkut.Documents.Services;

namespace Korkut.Infrastructure.Export;

/// <summary>
/// Renders markdown to Word or UYAP through <see cref="MarkdownDocumentExporter"/>. The
/// renderer stays static (it is a pure transformation); this is the port's face for it.
/// </summary>
public sealed class MarkdownExporter : IDocumentExporter
{
    public ExportedDocument Export(string format, string title, string markdown)
        => MarkdownDocumentExporter.Export(format, title, markdown);
}

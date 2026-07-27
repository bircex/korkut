namespace Korkut.Application.Export;

/// <summary>Renders markdown into a downloadable document. The rendering itself is an adapter concern.</summary>
public interface IDocumentExporter
{
    ExportedDocument Export(string format, string title, string markdown);
}

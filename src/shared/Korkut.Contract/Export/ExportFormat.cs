namespace Korkut.Contract.Export;

/// <summary>The file formats a document can be exported to.</summary>
public static class ExportFormat
{
    /// <summary>Word, for anything that will be edited or printed.</summary>
    public const string Docx = "docx";

    /// <summary>UYAP's own format, for filing with the courts.</summary>
    public const string Udf = "udf";

    public static bool IsSupported(string? format) => format is Docx or Udf;
}

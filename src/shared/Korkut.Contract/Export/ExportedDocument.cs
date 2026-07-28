namespace Korkut.Contract.Export;

/// <summary>An exported document: its bytes, MIME type, and a suggested download filename.</summary>
public sealed record ExportedDocument(byte[] Bytes, string ContentType, string FileName);

namespace Korkut.Contract.Documents.Responses;

/// <summary>A document as the file list shows it; the object key and checksum stay internal.</summary>
public sealed record DocumentResponse(
    Guid Id, string OriginalFilename, string ContentType, long Size, string Status, int Version, DateTimeOffset CreatedAt);

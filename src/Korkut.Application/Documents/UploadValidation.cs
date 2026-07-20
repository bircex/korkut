namespace Korkut.Application.Documents;

public enum UploadRejection
{
    None,
    TooLarge,
    DisallowedExtension,
    ContentTypeMismatch, // magic bytes don't match the claimed/declared type
    UnsafeFilename,      // path traversal / control chars
    ArchiveTooDeepOrLarge, // decompression-bomb guard
    Malware,
    ScannerUnavailable,  // fail-closed: cannot prove clean
}

public sealed record UploadCheck(bool Ok, UploadRejection Rejection, string? SafeFilename = null, string? DetectedType = null);

/// <summary>
/// Malware scanning result. The scanner is FAIL-CLOSED: any error/unavailability
/// yields <see cref="ScannerUnavailable"/>, never a "clean".
/// </summary>
public enum ScanResult
{
    Clean,
    Infected,
    ScannerUnavailable,
}

public interface IMalwareScanner
{
    Task<ScanResult> ScanAsync(Stream content, CancellationToken ct = default);
}

namespace Korkut.Application.Upload.Validator;

public enum UploadValidatorRejection
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

namespace Korkut.Contract.Upload.Validator;

/// <summary>
/// Layered upload checks BEFORE anything is trusted: size, extension allowlist,
/// magic-byte type sniffing (never the client's declared MIME), filename
/// normalization + path-traversal rejection, and a decompression-bomb guard for
/// archives. All of these run while the object is still in quarantine (KDM-026).
/// </summary>
public interface IUploadValidatorService
{
    UploadValidationResult Validate(string declaredFilename, Stream content);
}

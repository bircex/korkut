namespace Korkut.Application.Upload.Validator;

public sealed record UploadValidationResult(bool Ok, UploadValidatorRejection ValidatorRejection, string? SafeFilename = null, string? DetectedType = null);

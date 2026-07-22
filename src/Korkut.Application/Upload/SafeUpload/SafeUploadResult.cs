using Korkut.Application.Upload.Validator;

namespace Korkut.Application.Upload.SafeUpload;

public sealed record SafeUploadResult(SafeUploadStatus Status, Guid? DocumentId, UploadValidatorRejection ValidatorRejection);

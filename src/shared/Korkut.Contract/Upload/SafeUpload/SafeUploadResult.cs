using Korkut.Contract.Upload.Validator;

namespace Korkut.Contract.Upload.SafeUpload;

public sealed record SafeUploadResult(SafeUploadStatus Status, Guid? DocumentId, UploadValidatorRejection ValidatorRejection);

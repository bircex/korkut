namespace Korkut.Application.Upload.SafeUpload;

public enum SafeUploadStatus
{
    Clean,
    Rejected,
    Pending, // scanner unavailable — fail-closed, awaiting a successful scan
}

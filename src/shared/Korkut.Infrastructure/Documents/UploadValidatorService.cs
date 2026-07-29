using System.IO.Compression;
using Korkut.Contract.Upload;
using Korkut.Contract.Upload.SafeUpload;
using Korkut.Contract.Upload.Validator;

namespace Korkut.Infrastructure.Documents;

/// <summary>
/// Layered upload checks BEFORE anything is trusted: size, extension allowlist,
/// magic-byte type sniffing (never the client's declared MIME), filename
/// normalization + path-traversal rejection, and a decompression-bomb guard for
/// archives. All of these run while the object is still in quarantine (KDM-026).
/// </summary>
public sealed class UploadValidatorService : IUploadValidatorService
{
    private long MaxBytes { get; init; } = 25 * 1024 * 1024;

    private long MaxArchiveUncompressedBytes { get; init; } = 100 * 1024 * 1024;

    private int MaxArchiveRatio { get; init; } = 100; // uncompressed:compressed

    private int MaxArchiveEntries { get; init; } = 2_000;

    // Extension -> the magic-byte prefixes that a real file of that type must start with.
    private static readonly Dictionary<string, byte[][]> Signatures = new()
    {
        [".pdf"] = [[0x25, 0x50, 0x44, 0x46]], // %PDF
        [".png"] = [[0x89, 0x50, 0x4E, 0x47]],
        [".jpg"] = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
        [".docx"] = [[0x50, 0x4B, 0x03, 0x04]], // zip container
        [".xlsx"] = [[0x50, 0x4B, 0x03, 0x04]],
        [".zip"] = [[0x50, 0x4B, 0x03, 0x04]],
    };

    public UploadValidationResult Validate(string declaredFilename, Stream content)
    {
        if (content.Length > MaxBytes)
        {
            return new UploadValidationResult(false, UploadValidatorRejection.TooLarge);
        }

        var safe = SafeFilename(declaredFilename);
        if (safe is null)
        {
            return new UploadValidationResult(false, UploadValidatorRejection.UnsafeFilename);
        }

        var ext = Path.GetExtension(safe).ToLowerInvariant();
        if (!Signatures.TryGetValue(ext, out var magics))
        {
            return new UploadValidationResult(false, UploadValidatorRejection.DisallowedExtension, safe);
        }

        // Type is decided by the BYTES, not the client's declaration.
        var header = ReadHeader(content, 8);
        if (!magics.Any(m => header.AsSpan(0, Math.Min(header.Length, m.Length)).SequenceEqual(m)))
        {
            return new UploadValidationResult(false, UploadValidatorRejection.ContentTypeMismatch, safe);
        }

        // Zip-family: guard against decompression bombs by reading the central directory.
        if (header.Length >= 4 && header[0] == 0x50 && header[1] == 0x4B && !IsArchiveSafe(content))
        {
            return new UploadValidationResult(false, UploadValidatorRejection.ArchiveTooDeepOrLarge, safe);
        }

        return new UploadValidationResult(true, UploadValidatorRejection.None, safe, ext.TrimStart('.'));
    }

    /// <summary>Strip directories/control chars; reject traversal or empty results.</summary>
    private static string? SafeFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename) || filename.Contains('\0'))
        {
            return null;
        }

        // Take only the leaf; a traversal attempt like ../../etc/passwd collapses to "passwd".
        var leaf = filename.Replace('\\', '/');
        leaf = leaf[(leaf.LastIndexOf('/') + 1)..];
        var cleaned = new string(leaf.Where(c => !char.IsControl(c)).ToArray()).Trim();
        
        if (cleaned is "" or "." or ".." || cleaned.Contains(".."))
        {
            return null;
        }

        return cleaned.Length > 255 ? cleaned[^255..] : cleaned;
    }

    private bool IsArchiveSafe(Stream content)
    {
        content.Position = 0;
        try
        {
            using var zip = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
            if (zip.Entries.Count > MaxArchiveEntries)
            {
                return false;
            }

            long uncompressed = 0, compressed = 0;
            foreach (var entry in zip.Entries)
            {
                // Reject path traversal inside the archive too.
                if (entry.FullName.Contains("..") || entry.FullName.StartsWith('/'))
                {
                    return false;
                }

                uncompressed += entry.Length;
                compressed += entry.CompressedLength;
            }

            if (uncompressed > MaxArchiveUncompressedBytes)
            {
                return false;
            }

            return compressed <= 0 || uncompressed / Math.Max(1, compressed) <= MaxArchiveRatio;
        }
        catch (InvalidDataException)
        {
            return false; // not a real archive despite the PK header
        }
        finally
        {
            content.Position = 0;
        }
    }

    private static byte[] ReadHeader(Stream content, int n)
    {
        content.Position = 0;
        var buf = new byte[n];
        var read = content.Read(buf, 0, n);
        content.Position = 0;
        return read == n ? buf : buf[..read];
    }
}

using System.IO.Compression;
using System.Text;
using Korkut.Contract.Upload;
using Korkut.Contract.Upload.SafeUpload;
using Korkut.Contract.Upload.Validator;

namespace Korkut.Documents.Services;

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
    // .txt has no magic bytes (any byte sequence can be plain text) so it is handled separately.
    private static readonly Dictionary<string, byte[][]> Signatures = new()
    {
        [".pdf"] = [[0x25, 0x50, 0x44, 0x46]], // %PDF
        [".png"] = [[0x89, 0x50, 0x4E, 0x47]],
        [".jpg"] = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]],
        [".docx"] = [[0x50, 0x4B, 0x03, 0x04]], // zip container
        [".xlsx"] = [[0x50, 0x4B, 0x03, 0x04]],
        [".zip"] = [[0x50, 0x4B, 0x03, 0x04]],
        [".udf"] = [[0x50, 0x4B, 0x03, 0x04]], // UYAP document format — also a zip container
        [".doc"] = [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]], // legacy OLE2 compound file
    };

    private const string TextExtension = ".txt";

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
        if (ext == TextExtension)
        {
            return LooksLikeText(content)
                ? new UploadValidationResult(true, UploadValidatorRejection.None, safe, ext.TrimStart('.'))
                : new UploadValidationResult(false, UploadValidatorRejection.ContentTypeMismatch, safe);
        }

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

    /// <summary>
    /// .txt has no magic-byte signature — any byte sequence is technically valid text — so
    /// "the bytes decide the type" instead means: this sample is not obviously binary. A file
    /// misdeclared as .txt (e.g. an .exe or .pdf renamed) is rejected because binary content
    /// almost always contains a NUL byte early on or fails strict UTF-8 decoding.
    /// </summary>
    private static bool LooksLikeText(Stream content)
    {
        var sample = ReadHeader(content, 4096);
        if (sample.Length == 0)
        {
            return true; // an empty file is valid (empty) text
        }

        if (Array.IndexOf(sample, (byte)0) >= 0)
        {
            return false;
        }

        // A 4096-byte sample can cut a multi-byte UTF-8 character in half at the boundary — that
        // is a truncation artifact, not invalid text. UTF-8 characters are at most 4 bytes, so
        // retry against a slightly shorter prefix before concluding the content isn't text.
        var strict = new UTF8Encoding(false, throwOnInvalidBytes: true);
        for (var trim = 0; trim <= 3 && trim < sample.Length; trim++)
        {
            try
            {
                strict.GetString(sample.AsSpan(0, sample.Length - trim));
                return true;
            }
            catch (DecoderFallbackException)
            {
                // try a shorter prefix
            }
        }

        return false;
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

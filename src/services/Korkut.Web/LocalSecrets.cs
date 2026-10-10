using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace Korkut.Web;

/// <summary>
/// Lets a local install start with no configuration: a host secret that is not set in the
/// environment is generated once, kept in a file under <see cref="DirectorySetting"/> (a volume
/// in compose), and read back on every later start. A value set in the environment always wins.
/// Every host that needs the same secret points at the same directory, and a file is only ever
/// created atomically, never replaced, so hosts starting together still end up with one value.
/// </summary>
public static class LocalSecrets
{
    public const string DirectorySetting = "KORKUT_SECRETS_DIR";
    public const string DefaultDirectory = "/var/lib/korkut/secrets";

    // Setting name -> how a fresh value is made. Both are 32 random bytes: KORKUT_SECRET as hex
    // (an HMAC key must be at least 32 bytes), KORKUT_TOTP_KEY as base64 (an AES-256 key).
    private static readonly (string Key, Func<string> Generate)[] Generated =
    [
        ("KORKUT_SECRET", () => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()),
        ("KORKUT_TOTP_KEY", () => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))),
    ];

    /// <summary>
    /// Fills in each missing secret from its file, creating the file first when there is none.
    /// When the directory cannot be used (a developer machine without it), the setting stays
    /// missing and the host's own fail-fast check reports it, exactly as before.
    /// </summary>
    public static IConfigurationManager AddKorkutLocalSecrets(this IConfigurationManager configuration)
    {
        var directory = configuration[DirectorySetting];
        directory = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory;

        var values = new Dictionary<string, string?>();
        foreach (var (key, generate) in Generated)
        {
            if (string.IsNullOrWhiteSpace(configuration[key]) && TryLoadOrCreate(directory, key, generate) is { } value)
            {
                values[key] = value;
            }
        }

        if (values.Count > 0)
        {
            configuration.AddInMemoryCollection(values);
        }

        return configuration;
    }

    internal static string? TryLoadOrCreate(string directory, string key, Func<string> generate)
    {
        var path = Path.Combine(directory, key.ToLowerInvariant());
        var fresh = generate();
        try
        {
            Directory.CreateDirectory(directory);
            try
            {
                // CreateNew is the OS's atomic "create only if absent" (O_EXCL): of hosts starting
                // together exactly one writes, and its value is the one everybody reads.
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.Read };
                if (!OperatingSystem.IsWindows())
                {
                    // Readable by the app user only; every host that shares it runs as that user.
                    options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                }

                using var file = new FileStream(path, options);
                file.Write(System.Text.Encoding.UTF8.GetBytes(fresh));
                file.Flush(flushToDisk: true);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Created by another host, or on an earlier start.
            }

            // The winner may still be writing; a value is whole once it has the length every
            // generated value has (the formats are fixed-length).
            for (var attempt = 0; attempt < 50; attempt++)
            {
                try
                {
                    var value = File.ReadAllText(path).Trim();
                    if (value.Length == fresh.Length)
                    {
                        return value;
                    }
                }
                catch (IOException)
                {
                    // Still open for writing by the host that created it.
                }

                Thread.Sleep(20);
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

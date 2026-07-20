using System.Text.RegularExpressions;

namespace Korkut.Application.Runtime;

/// <summary>
/// Redacts secret-shaped environment values before any config is logged or dumped
/// (KDM-033). Provider API keys, DB/SMTP passwords and signing secrets must NEVER
/// appear in logs, diagnostics, or an admin surface — only the variable NAME and a
/// masked marker are ever emitted.
/// </summary>
public static partial class ConfigRedactor
{
    // A variable is treated as secret if its NAME looks like a credential holder.
    [GeneratedRegex(@"(API_KEY|APIKEY|SECRET|PASSWORD|TOKEN|CLIENT_SECRET)$", RegexOptions.IgnoreCase)]
    private static partial Regex SecretNameRegex();

    public const string Mask = "***REDACTED***";

    public static bool IsSecretName(string name) => SecretNameRegex().IsMatch(name);

    /// <summary>Return a copy of an env map safe to log: secret VALUES are masked.</summary>
    public static IReadOnlyDictionary<string, string> RedactMap(IReadOnlyDictionary<string, string> env)
    {
        var outp = new Dictionary<string, string>(env.Count, StringComparer.Ordinal);
        foreach (var (name, value) in env)
        {
            outp[name] = IsSecretName(name) && !string.IsNullOrEmpty(value) ? Mask : value;
        }

        return outp;
    }
}

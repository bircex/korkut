using System.Security.Cryptography;
using System.Text;
using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;

namespace Korkut.Application.Agent.Models;

/// <summary>
/// The versioned fingerprint of a composed prompt (system prompt + persona + style + grounding),
/// stamped onto each call's telemetry so a quality/cost shift can be traced to a prompt change and
/// A/B'd or rolled back (principle 9). A short, stable hash — not the prompt itself (never logged).
/// </summary>
public static class PromptFingerprint
{
    public static string Of(string composedPrompt)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(composedPrompt ?? string.Empty));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}

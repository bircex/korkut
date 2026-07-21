using System.Security.Cryptography;
using System.Text;

namespace Korkut.Application.Ai;

/// <summary>Raised when an office has exhausted its model-usage budget; surfaced as 429.</summary>
public sealed class BudgetExceededException(string message) : Exception(message);

/// <summary>
/// Cost/rate governance for model calls (principle 9). Before a call, the caller enters the
/// governor: it enforces the per-office <b>token budget</b> (derived from the <c>llm_calls</c>
/// ledger) and the per-office <b>concurrency cap</b>. The returned handle MUST be disposed when the
/// call finishes to release the concurrency slot. Both limits are opt-in (unset → unlimited).
/// </summary>
public interface IUsageGovernor
{
    Task<IAsyncDisposable> BeginCallAsync(Guid officeId, CancellationToken ct = default);
}

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

using System.Globalization;

namespace Korkut.Core;

/// <summary>
/// The model environment contract. Which providers and models answer is not configured here: the
/// model gateway (llmr) holds that, set from the app's AI settings. Korkut only knows where the
/// gateway is, the names it asks for, and its own context budget.
/// </summary>
public sealed class RuntimeConfig(IEnvironment env)
{
    // --- The model gateway (llmr) ---
    /// <summary>Where llmr listens, e.g. <c>http://llmr:8080</c>; unset, model calls report it unconfigured.</summary>
    public string? LlmrUrl => env.Get("KORKUT_LLMR_URL")?.Trim() is { Length: > 0 } url ? url.TrimEnd('/') : null;

    /// <summary>Largest reply Korkut asks the chat model for.</summary>
    public int ChatMaxTokens => ParseInt(env.Get("KORKUT_MAX_TOKENS"), 8192);

    // --- Context budget ---
    public int ContextWindow => ParseInt(env.Get("KORKUT_CONTEXT_WINDOW"), 128000);

    public int LocalContextWindow => ParseInt(env.Get("KORKUT_LOCAL_CONTEXT_WINDOW"), 8192);

    public int ReplyHeadroom => ParseInt(env.Get("KORKUT_REPLY_HEADROOM"), 1024);

    /// <summary>
    /// Safety ceiling on model turns per chat request in the tool loop — the runaway brake, not a
    /// depth budget (one turn may carry several tool calls). High enough that real research never
    /// hits it; a looping model does, before it burns the office's token budget.
    /// </summary>
    public int MaxToolTurns => ParseInt(env.Get("KORKUT_MAX_TOOL_TURNS"), 32);

    // --- Governance (Phase 10): per-office model-usage limits. 0/unset = unlimited. ---
    /// <summary>Per-office token budget per calendar month (prompt + completion); 0 = unlimited.</summary>
    public int OfficeTokenBudget => ParseInt(env.Get("KORKUT_OFFICE_TOKEN_BUDGET"), 0);

    /// <summary>Max concurrent in-flight model calls per office (per replica); 0 = unlimited.</summary>
    public int OfficeConcurrency => ParseInt(env.Get("KORKUT_OFFICE_CONCURRENCY"), 0);

    private static int ParseInt(string? raw, int fallback)
    {
        if (raw is null)
        {
            return fallback;
        }

        // Parity with Python int(os.getenv(...)): a malformed value is a hard error,
        // not a silent fallback.
        return int.Parse(raw.Trim(), CultureInfo.InvariantCulture);
    }
}

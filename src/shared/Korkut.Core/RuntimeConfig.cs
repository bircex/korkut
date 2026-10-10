using System.Globalization;

namespace Korkut.Core;

/// <summary>
/// The LLM-provider + context-budget environment contract, ported 1:1 from the Python
/// baseline (docs/migration/baseline/ENV_CONTRACT.md, KDM-002/033). Every variable
/// keeps its EXACT name and default. API keys are NEVER exposed as values here — only
/// their presence (<c>Has*Key</c>) — because keys stay operator-managed in the
/// environment and are consumed directly by the vendor SDKs, never surfaced to DB/UI
/// or logs.
/// </summary>
public sealed class RuntimeConfig(IEnvironment env)
{
    // --- Provider model ids + limits (exact Python names/defaults) ---
    public string AnthropicModel => env.Get("ANTHROPIC_MODEL") ?? "claude-opus-4-8";

    public int AnthropicMaxTokens => ParseInt(env.Get("ANTHROPIC_MAX_TOKENS"), 16000);

    public string OpenAiModel => env.Get("OPENAI_MODEL") ?? "gpt-4o";

    public string GroqModel => env.Get("GROQ_MODEL") ?? "llama-3.3-70b-versatile";

    // DeepSeek is OpenAI-compatible (base https://api.deepseek.com). deepseek-chat = V3, deepseek-reasoner = R1.
    public string DeepSeekModel => env.Get("DEEPSEEK_MODEL") ?? "deepseek-chat";

    public string DeepSeekBaseUrl => env.Get("DEEPSEEK_BASE_URL")?.Trim() is { Length: > 0 } url ? url : "https://api.deepseek.com";

    public int DeepSeekMaxTokens => ParseInt(env.Get("DEEPSEEK_MAX_TOKENS"), 8000);

    // --- Context budget (tokens) ---
    public int ContextWindow => ParseInt(env.Get("KORKUT_CONTEXT_WINDOW"), 128000);

    public int LocalContextWindow => ParseInt(env.Get("KORKUT_LOCAL_CONTEXT_WINDOW"), 8192);

    public int ReplyHeadroom => ParseInt(env.Get("KORKUT_REPLY_HEADROOM"), 1024);

    /// <summary>
    /// Safety ceiling on model turns per chat request in the tool loop — the runaway brake, not a
    /// depth budget (one turn may carry several tool calls). High enough that real research never
    /// hits it; a looping model does, before it burns the office's token budget.
    /// </summary>
    public int MaxToolTurns => ParseInt(env.Get("KORKUT_MAX_TOOL_TURNS"), 32);

    // --- Embeddings (Knowledge/RAG). Prefer a fully-local endpoint (Ollama / HF-TEI) so office
    //     data never leaves the cluster (docs/SOURCES.md); a hosted key is optional. The base URL's
    //     presence is what enables the real embedder — no URL → embeddings stay disabled. ---
    public string? EmbeddingBaseUrl => env.Get("KORKUT_EMBEDDING_URL")?.Trim() is { Length: > 0 } url ? url : null;

    public string EmbeddingModel => env.Get("KORKUT_EMBEDDING_MODEL") ?? "nomic-embed-text";

    public bool HasEmbedding => !string.IsNullOrWhiteSpace(EmbeddingBaseUrl);

    // --- Governance (Phase 10): per-office model-usage limits. 0/unset = unlimited. ---
    /// <summary>Per-office token budget per calendar month (prompt + completion); 0 = unlimited.</summary>
    public int OfficeTokenBudget => ParseInt(env.Get("KORKUT_OFFICE_TOKEN_BUDGET"), 0);

    /// <summary>Max concurrent in-flight model calls per office (per replica); 0 = unlimited.</summary>
    public int OfficeConcurrency => ParseInt(env.Get("KORKUT_OFFICE_CONCURRENCY"), 0);

    // --- Key PRESENCE only — never the value. Consumed by the vendor SDKs from env. ---
    public bool HasAnthropicKey => IsSet("ANTHROPIC_API_KEY");

    public bool HasOpenAiKey => IsSet("OPENAI_API_KEY");

    public bool HasGroqKey => IsSet("GROQ_API_KEY");

    public bool HasDeepSeekKey => IsSet("DEEPSEEK_API_KEY");


    private bool IsSet(string name) => !string.IsNullOrWhiteSpace(env.Get(name));

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

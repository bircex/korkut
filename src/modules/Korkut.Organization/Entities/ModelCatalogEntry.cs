using Korkut.Core;

namespace Korkut.Organization.Entities;

/// <summary>
/// One entry in the platform's model catalog — the DB-backed replacement for the operator's
/// read-only KORKUT_MODELS_FILE. An admin edits these from the console, so adding a model no
/// longer means editing a file and redeploying. API keys are NEVER stored here: <see cref="Provider"/>
/// names a registered IChatProviderService, which reads its key from the environment at call time.
/// </summary>
public sealed class ModelCatalogEntry : Entity, IAggregateRoot
{
    public ModelCatalogEntry(
        string key, string label, string provider, string modelId, string? baseUrl, int? maxTokens,
        bool isLocal, bool enabled, int sortOrder, DateTimeOffset now)
        : base(NewId())
    {
        Key = key;
        Label = label;
        Provider = provider;
        ModelId = modelId;
        BaseUrl = baseUrl;
        MaxTokens = maxTokens;
        IsLocal = isLocal;
        Enabled = enabled;
        SortOrder = sortOrder;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private ModelCatalogEntry()
    {
    }

    /// <summary>Stable identifier an office assignment / the platform default points at.</summary>
    public string Key { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    /// <summary>The registered IChatProviderService name: claude | deepseek | gemini | gpt | groq | ollama.</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>The provider's own model id (e.g. "deepseek-chat", "gemini-3.5-flash").</summary>
    public string ModelId { get; private set; } = string.Empty;

    /// <summary>Informational for the console; the provider's transport base URL stays operator-set.</summary>
    public string? BaseUrl { get; private set; }

    /// <summary>Per-model output cap; null falls back to the router's default.</summary>
    public int? MaxTokens { get; private set; }

    public bool IsLocal { get; private set; }

    /// <summary>A disabled entry stays in the catalog but can no longer be assigned or defaulted to.</summary>
    public bool Enabled { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string label, string provider, string modelId, string? baseUrl, int? maxTokens,
        bool isLocal, bool enabled, int sortOrder, DateTimeOffset now)
    {
        Label = label;
        Provider = provider;
        ModelId = modelId;
        BaseUrl = baseUrl;
        MaxTokens = maxTokens;
        IsLocal = isLocal;
        Enabled = enabled;
        SortOrder = sortOrder;
        UpdatedAt = now;
    }
}

using Korkut.Application.Ai;
using Korkut.Agent.Application;
using Korkut.Application.Runtime;

namespace Korkut.Agent.Infrastructure;

/// <summary>
/// Resolves a specialty to a provider + model using the ported <see cref="ModelRouting"/>
/// (specialty → tier → provider name) and <see cref="RuntimeConfig"/> (provider → model id +
/// token cap). The local-availability probe drives the <c>fallback_to_cloud_if_no_local</c>
/// rule. Provider names match the yaml vocabulary: claude | gpt | groq | ollama.
/// </summary>
public sealed class ModelRouter(ModelRouting routing, RuntimeConfig config, IEnvironment env, ILocalModelProbe local) : IModelRouter
{
    /// <summary>Token cap for providers whose max isn't part of the ported RuntimeConfig contract.</summary>
    private const int DefaultMaxTokens = 4096;

    public ModelSelection Resolve(string specialty, IReadOnlySet<string> availableProviders)
    {
        var tier = routing.ResolveTier(specialty, local.IsAvailable);
        var provider = routing.ProviderForTier(tier, availableProviders);
        var (modelId, maxTokens) = ModelFor(provider);
        return new ModelSelection(provider, modelId, maxTokens);
    }

    private (string ModelId, int MaxTokens) ModelFor(string provider) => provider switch
    {
        "claude" => (config.AnthropicModel, config.AnthropicMaxTokens),
        "gpt" => (config.OpenAiModel, DefaultMaxTokens),
        "groq" => (config.GroqModel, DefaultMaxTokens),
        // Local (Ollama) model id is operator-set; kept off the frozen RuntimeConfig contract.
        "ollama" => (env.Get("KORKUT_LOCAL_MODEL") ?? "llama3.1", DefaultMaxTokens),
        _ => throw new ModelUnavailableException($"Bilinmeyen sağlayıcı: {provider}"),
    };
}

/// <summary>Default probe: no local backend until the GPU/Ollama host is wired (always false).</summary>
public sealed class LocalModelUnavailable : ILocalModelProbe
{
    public bool IsAvailable => false;
}

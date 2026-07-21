using Korkut.Application.Ai;

namespace Korkut.Infrastructure.Ai;

/// <summary>
/// The <see cref="IChatModel"/> consumers depend on. Per request it asks the router which
/// provider + model to use (from the specialty), then delegates to the matching
/// <see cref="IChatProvider"/>. This is the single seam where routing lives; providers and
/// consumers stay ignorant of each other. (Cost/telemetry will read <see cref="ChatReply.Usage"/>
/// here in the observability phase — no port change needed.)
/// </summary>
public sealed class RoutingChatModel : IChatModel
{
    private readonly IModelRouter router;
    private readonly IReadOnlyDictionary<string, IChatProvider> providers;
    private readonly IReadOnlySet<string> providerNames;

    public RoutingChatModel(IModelRouter router, IEnumerable<IChatProvider> providers)
    {
        this.router = router;
        this.providers = providers.ToDictionary(p => p.Name);
        this.providerNames = this.providers.Keys.ToHashSet();
    }

    public Task<ChatReply> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        var (provider, selection) = Pick(request.Specialty);
        return provider.CompleteAsync(selection, request, ct);
    }

    public IAsyncEnumerable<ChatStreamEvent> StreamAsync(ChatRequest request, CancellationToken ct = default)
    {
        var (provider, selection) = Pick(request.Specialty);
        return provider.StreamAsync(selection, request, ct);
    }

    private (IChatProvider Provider, ModelSelection Selection) Pick(string specialty)
    {
        var selection = router.Resolve(specialty, providerNames);
        return providers.TryGetValue(selection.Provider, out var provider)
            ? (provider, selection)
            : throw new ModelUnavailableException($"Sağlayıcı kayıtlı değil: {selection.Provider}");
    }
}

/// <summary>Fallback used when no provider is configured — every call reports unavailable (503).</summary>
public sealed class DisabledChatModel : IChatModel
{
    public Task<ChatReply> CompleteAsync(ChatRequest request, CancellationToken ct = default) =>
        throw new ModelUnavailableException("Yapay zeka sağlayıcısı yapılandırılmamış.");

    public IAsyncEnumerable<ChatStreamEvent> StreamAsync(ChatRequest request, CancellationToken ct = default) =>
        throw new ModelUnavailableException("Yapay zeka sağlayıcısı yapılandırılmamış.");
}

/// <summary>Fallback embedder until the real one lands with the Knowledge service.</summary>
public sealed class DisabledEmbedder : IEmbedder
{
    public Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default) =>
        throw new ModelUnavailableException("Embedding sağlayıcısı yapılandırılmamış.");
}

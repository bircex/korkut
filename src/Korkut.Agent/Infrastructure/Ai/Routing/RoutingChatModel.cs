using Korkut.Agent.Application.Ai;
using Korkut.Agent.Application.Abstractions.Ai;
using Korkut.Agent.Application.Abstractions.Persistence;
using Korkut.Agent.Application.Abstractions.Sessions;
using Korkut.Agent.Application.Sessions;
using Korkut.Agent.Application.Chat;
using Korkut.Application.Ai;

namespace Korkut.Agent.Infrastructure.Ai.Routing;

/// <summary>
/// The <see cref="IChatModel"/> consumers depend on. Per request it asks the router which
/// provider + model to use (from the specialty), then delegates to the matching
/// <see cref="IChatProvider"/>. This is the single seam where routing lives; providers and
/// consumers stay ignorant of each other.
/// </summary>
public sealed class RoutingChatModel : IChatModel
{
    private readonly IModelRouter _router;
    private readonly IReadOnlyDictionary<string, IChatProvider> _providers;
    private readonly IReadOnlySet<string> _providerNames;

    public RoutingChatModel(IModelRouter router, IEnumerable<IChatProvider> providers)
    {
        _router = router;
        _providers = providers.ToDictionary(p => p.Name);
        _providerNames = _providers.Keys.ToHashSet();
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
        var selection = _router.Resolve(specialty, _providerNames);
        return _providers.TryGetValue(selection.Provider, out var provider)
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

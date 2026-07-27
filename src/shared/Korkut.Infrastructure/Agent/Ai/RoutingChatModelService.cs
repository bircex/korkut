using Korkut.Application.Ai;
using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;

namespace Korkut.Infrastructure.Agent.Ai;

/// <summary>
/// The <see cref="IChatModelService"/> consumers depend on. Per request it asks the router which
/// provider + model to use (from the specialty), then delegates to the matching
/// <see cref="IChatProviderService"/>. This is the single seam where routing lives; providers and
/// consumers stay ignorant of each other.
/// </summary>
public sealed class RoutingChatModelService : IChatModelService
{
    private readonly IModelRouterService _routerService;
    private readonly IReadOnlyDictionary<string, IChatProviderService> _providers;
    private readonly IReadOnlySet<string> _providerNames;

    public RoutingChatModelService(IModelRouterService routerService, IEnumerable<IChatProviderService> providers)
    {
        _routerService = routerService;
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

    private (IChatProviderService Provider, ModelSelection Selection) Pick(string specialty)
    {
        var selection = _routerService.Resolve(specialty, _providerNames);
        return _providers.TryGetValue(selection.Provider, out var provider)
            ? (provider, selection)
            : throw new ModelUnavailableException($"Sağlayıcı kayıtlı değil: {selection.Provider}");
    }
}

/// <summary>Fallback used when no provider is configured — every call reports unavailable (503).</summary>
public sealed class DisabledChatModelService : IChatModelService
{
    public Task<ChatReply> CompleteAsync(ChatRequest request, CancellationToken ct = default) =>
        throw new ModelUnavailableException("Yapay zeka sağlayıcısı yapılandırılmamış.");

    public IAsyncEnumerable<ChatStreamEvent> StreamAsync(ChatRequest request, CancellationToken ct = default) =>
        throw new ModelUnavailableException("Yapay zeka sağlayıcısı yapılandırılmamış.");
}

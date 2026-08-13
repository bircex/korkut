using System.Runtime.CompilerServices;
using Korkut.Contract.Agent.Model;
using Korkut.Core;
using ICurrentUser = Korkut.Contract.Agent.ICurrentUser;

namespace Korkut.Agent.Model.Services;

/// <summary>
/// The <see cref="IChatModelService"/> consumers depend on. Fully agnostic: nothing here (or
/// anywhere downstream) hardcodes a preferred vendor. Per request it applies the precedence chain
/// documented on <see cref="IOfficeModelOverrideService"/> — office pin, then a local backend when
/// this specialty wants one and it's reachable, then the dynamic system default — and delegates to
/// the matching <see cref="IChatProviderService"/>. When none of those resolve, the office has no
/// usable model and the request fails loudly rather than silently guessing a provider; an admin
/// fixes it from the console, live, without a restart.
/// </summary>
internal sealed class RoutingChatModelService : IChatModelService
{
    /// <summary>Cap for an office/system-default selection, which (unlike the local backend) has no
    /// per-provider RuntimeConfig max to fall back to.</summary>
    private const int OverrideMaxTokens = 4096;

    private readonly IModelRouterService _routerService;
    private readonly IOfficeModelOverrideService _overrides;
    private readonly ICurrentUser _currentUser;
    private readonly Dictionary<string, IChatProviderService> _providers;

    public RoutingChatModelService(
        IModelRouterService routerService, IOfficeModelOverrideService overrides, ICurrentUser currentUser,
        IEnumerable<IChatProviderService> providers)
    {
        _routerService = routerService;
        _overrides = overrides;
        _currentUser = currentUser;
        _providers = providers.ToDictionary(p => p.Name);
    }

    public async Task<ChatReply> CompleteAsync(ChatRequest request, CancellationToken ct = default)
    {
        var (provider, selection) = await PickAsync(request.Specialty, ct);
        return await provider.CompleteAsync(selection, request, ct);
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var (provider, selection) = await PickAsync(request.Specialty, ct);
        await foreach (var ev in provider.StreamAsync(selection, request, ct))
        {
            yield return ev;
        }
    }

    private async Task<(IChatProviderService Provider, ModelSelection Selection)> PickAsync(string specialty, CancellationToken ct)
    {
        var office = await _overrides.ResolveOfficeAsync(_currentUser.OfficeId, ct);
        if (office is { } o)
        {
            return Resolve(new ModelSelection(o.Provider, o.ModelId, o.MaxTokens ?? OverrideMaxTokens));
        }

        if (_routerService.ResolveLocal(specialty) is { } local)
        {
            return Resolve(local);
        }

        var systemDefault = await _overrides.ResolveSystemDefaultAsync(ct);
        if (systemDefault is { } d)
        {
            return Resolve(new ModelSelection(d.Provider, d.ModelId, d.MaxTokens ?? OverrideMaxTokens));
        }

        throw new ModelUnavailableException(
            "Sistem varsayılan modeli henüz yapılandırılmamış. Lütfen yönetim panelinden bir model seçin.");
    }

    private (IChatProviderService, ModelSelection) Resolve(ModelSelection selection)
        => _providers.TryGetValue(selection.Provider, out var provider)
            ? (provider, selection)
            : throw new ModelUnavailableException($"Sağlayıcı kayıtlı değil: {selection.Provider}");
}

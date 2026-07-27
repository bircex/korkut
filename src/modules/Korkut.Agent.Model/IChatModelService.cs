
namespace Korkut.Agent.Model;

/// <summary>
/// The chat port every consumer depends on. Routing (specialty → provider + model) and
/// telemetry are invisible behind it — a consumer just sends a <see cref="ChatRequest"/>.
/// The implementation (RoutingChatModel) resolves the provider per request; swapping or
/// adding a provider never touches a consumer.
/// </summary>
public interface IChatModelService
{
    Task<ChatReply> CompleteAsync(ChatRequest request, CancellationToken ct = default);

    /// <summary>Stream the reply as <see cref="ChatStreamEvent"/>s (text deltas, then a terminal usage).</summary>
    IAsyncEnumerable<ChatStreamEvent> StreamAsync(ChatRequest request, CancellationToken ct = default);
}

using Korkut.Contract.Agent.Model;
using Korkut.Core;

namespace Korkut.Agent.Model.Services;

/// <summary>
/// The <see cref="IChatModelService"/> the reasoning layer calls. Every specialty asks the model
/// gateway for the same name, <see cref="ChatRoute"/>; which provider and model answer, and what
/// they fall back to, is llmr's route set, chosen from the app's AI settings.
/// </summary>
internal sealed class GatewayChatModelService(IChatProviderService provider, RuntimeConfig config) : IChatModelService
{
    /// <summary>The route set name Korkut's chat, drafting and tool calls go to.</summary>
    public const string ChatRoute = "korkut-chat";

    public Task<ChatReply> CompleteAsync(ChatRequest request, CancellationToken ct = default)
        => provider.CompleteAsync(Selection, request, ct);

    public IAsyncEnumerable<ChatStreamEvent> StreamAsync(ChatRequest request, CancellationToken ct = default)
        => provider.StreamAsync(Selection, request, ct);

    private ModelSelection Selection => new(provider.Name, ChatRoute, config.ChatMaxTokens);
}

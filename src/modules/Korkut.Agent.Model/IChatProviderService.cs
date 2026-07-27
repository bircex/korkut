
namespace Korkut.Agent.Model;

/// <summary>
/// One vendor adapter, keyed by <see cref="Name"/> (the token used in <c>model-routing.yaml</c>
/// backends: "claude" | "gpt" | "groq" | "ollama"). It receives an already-resolved
/// <see cref="ModelSelection"/> — it knows HTTP + vendor schema, nothing about routing. Adding a
/// provider = a new adapter with a new Name; no consumer changes.
/// </summary>
public interface IChatProviderService
{
    string Name { get; }

    Task<ChatReply> CompleteAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default);

    IAsyncEnumerable<ChatStreamEvent> StreamAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default);
}

namespace Korkut.Application.Ai;

/// <summary>One turn in an LLM conversation.</summary>
public sealed record LlmMessage(string Role, string Content);

/// <summary>A completion request: a system prompt, the conversation so far, and a token cap.</summary>
public sealed record LlmRequest(string System, IReadOnlyList<LlmMessage> Messages, int MaxTokens);

/// <summary>The assistant's reply text.</summary>
public sealed record LlmReply(string Text);

/// <summary>Raised when the LLM provider is not configured or a completion fails; surfaced as 503.</summary>
public sealed class LlmUnavailableException(string message) : Exception(message);

/// <summary>
/// Abstraction over the chat-completion provider. A real implementation calls the
/// vendor API (keys stay operator-managed in the environment, never in the DB/UI); tests
/// substitute a deterministic fake. Throws <see cref="LlmUnavailableException"/> when the
/// provider is unconfigured or errors.
/// </summary>
public interface ILlmClient
{
    Task<LlmReply> CompleteAsync(LlmRequest request, CancellationToken ct = default);
}

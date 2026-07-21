namespace Korkut.Application.Ai;

/// <summary>One turn in a chat conversation ("user" | "assistant"; the system prompt is separate).</summary>
public sealed record ChatMessage(string Role, string Content);

/// <summary>
/// A provider-agnostic chat request. The caller expresses INTENT via <see cref="Specialty"/>
/// (e.g. "petition") — never a concrete model id — and the router maps it to a provider +
/// model. <see cref="MaxTokens"/> is an optional caller cap (bounded by the provider's own
/// limit); omit to use the provider default.
/// </summary>
public sealed record ChatRequest(
    string System,
    IReadOnlyList<ChatMessage> Messages,
    string Specialty,
    int? MaxTokens = null,
    double? Temperature = null);

/// <summary>Token accounting for one model call — the seam telemetry/billing hangs off (principle 8/9).</summary>
public sealed record ChatUsage(string Provider, string ModelId, int PromptTokens, int CompletionTokens)
{
    public static readonly ChatUsage None = new(string.Empty, string.Empty, 0, 0);
}

/// <summary>A completed chat reply plus its usage.</summary>
public sealed record ChatReply(string Text, ChatUsage Usage);

/// <summary>
/// One streamed event: either a text <see cref="Delta"/> (mid-stream) or the terminal
/// <see cref="Usage"/> (once, at end). Exactly one of the two is set. Carrying usage on the
/// stream lets streamed calls be cost-accounted too, without a later port change.
/// </summary>
public sealed record ChatStreamEvent(string? Delta, ChatUsage? Usage)
{
    public static ChatStreamEvent Text(string delta) => new(delta, null);

    public static ChatStreamEvent Final(ChatUsage usage) => new(null, usage);
}

/// <summary>Raised when no provider is configured or a model call fails; surfaced as 503.</summary>
public sealed class ModelUnavailableException(string message) : Exception(message);

/// <summary>
/// The task intents the router understands (keys into <c>model-routing.yaml</c> specialties).
/// Callers pass one of these, never a model id.
/// </summary>
public static class ModelSpecialty
{
    public const string Dispatcher = "dispatcher";
    public const string Hearing = "hearing";
    public const string Petition = "petition";
    public const string Research = "research";
}

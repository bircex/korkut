namespace Korkut.Contract.Agent.Model;

/// <summary>A tool the model may call: its name, human description, and JSON-Schema for arguments.</summary>
public sealed record ToolDefinition(string Name, string Description, System.Text.Json.JsonElement Schema);

/// <summary>The model's request to call one tool: an id (to correlate the result), name, and JSON arguments.</summary>
public sealed record ToolInvocation(string Id, string Name, System.Text.Json.JsonElement Arguments);

/// <summary>A tool's result fed back to the model, correlated by <see cref="ToolCallId"/>.</summary>
public sealed record ToolOutput(string ToolCallId, string Content, bool IsError = false);

/// <summary>
/// One turn in a chat conversation. "user" | "assistant" carry <see cref="Content"/>; an assistant
/// turn may also carry <see cref="ToolCalls"/> (tools the model wants run); a "tool" turn carries
/// <see cref="ToolResults"/> (the outputs fed back). The optional tool fields keep older callers
/// (plain chat) unchanged.
/// </summary>
public sealed record ChatMessage(
    string Role,
    string Content,
    IReadOnlyList<ToolInvocation>? ToolCalls = null,
    IReadOnlyList<ToolOutput>? ToolResults = null);

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
    double? Temperature = null,
    IReadOnlyList<ToolDefinition>? Tools = null);

/// <summary>Token accounting for one model call — the seam telemetry/billing hangs off (principle 8/9).</summary>
public sealed record ChatUsage(string Provider, string ModelId, int PromptTokens, int CompletionTokens)
{
    public static readonly ChatUsage None = new(string.Empty, string.Empty, 0, 0);
}

/// <summary>
/// A completed chat reply plus its usage. When the model chose to call tools instead of (or before)
/// answering, <see cref="ToolCalls"/> is non-empty and the loop runs them before asking again.
/// <see cref="Sources"/> carries the legal sources the loop's tool calls surfaced (raw, in call
/// order; the chat service dedupes/caps before persisting or emitting them). <see cref="Document"/>
/// is the draft document a tool call produced this turn (the UI's editor-open signal), when any.
/// </summary>
public sealed record ChatReply(
    string Text, ChatUsage Usage, IReadOnlyList<ToolInvocation>? ToolCalls = null,
    IReadOnlyList<Chat.SourceRef>? Sources = null, Chat.DocumentRef? Document = null);

/// <summary>
/// One streamed event: a text <see cref="Delta"/> (mid-stream), the tool calls the model wants run
/// (<see cref="ToolCalls"/>, emitted once when the turn is a tool-use turn), or the terminal
/// <see cref="Usage"/> (once, at end). Carrying tool calls + usage on the stream lets the tool loop
/// run natively over the stream (real token-by-token answers) and still cost-account every call.
/// </summary>
public sealed record ChatStreamEvent(string? Delta, ChatUsage? Usage, IReadOnlyList<ToolInvocation>? ToolCalls = null)
{
    public static ChatStreamEvent Text(string delta) => new(delta, null);

    public static ChatStreamEvent Tools(IReadOnlyList<ToolInvocation> toolCalls) => new(null, null, toolCalls);

    public static ChatStreamEvent Final(ChatUsage usage) => new(null, usage);
}

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

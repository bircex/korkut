using Korkut.Contract.Agent.Chat;

namespace Korkut.Tools.Core;

/// <summary>
/// The outcome of a tool invocation, fed back to the model as the tool's result. A tool may also
/// attach the structured legal <see cref="Sources"/> behind its text (e.g. <c>search_decisions</c>
/// hits) or the draft <see cref="Document"/> it produced (<c>draft_document</c>); the loop
/// accumulates them for the UI — the model still only sees <see cref="Content"/>.
/// </summary>
public sealed record ToolResult(string Content, bool IsError = false)
{
    public IReadOnlyList<SourceRef>? Sources { get; init; }

    public DocumentRef? Document { get; init; }

    public static ToolResult Ok(string content) => new(content);

    public static ToolResult Ok(string content, IReadOnlyList<SourceRef>? sources) =>
        new(content) { Sources = sources };

    public static ToolResult Error(string content) => new(content, true);
}

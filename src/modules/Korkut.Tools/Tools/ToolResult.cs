namespace Korkut.Tools.Tools;

/// <summary>The outcome of a tool invocation, fed back to the model as the tool's result.</summary>
public sealed record ToolResult(string Content, bool IsError = false)
{
    public static ToolResult Ok(string content) => new(content);

    public static ToolResult Error(string content) => new(content, true);
}

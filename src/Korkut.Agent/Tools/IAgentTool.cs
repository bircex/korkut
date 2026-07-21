using System.Text.Json;

namespace Korkut.Agent.Tools;

/// <summary>
/// One capability the assistant can call. Self-describing (<see cref="Spec"/>), optionally gated
/// on an API key (<see cref="RequiresKey"/> — hidden when unset, per the ToolRegistry rule), and
/// executed within the caller's office (<see cref="ToolContext"/>, RLS). Adding a tool is a new
/// implementation of this interface plus one DI registration; the tool loop never changes.
/// </summary>
public interface IAgentTool
{
    ToolSpec Spec { get; }

    /// <summary>The environment variable this tool needs (e.g. an API key); null = always available.</summary>
    string? RequiresKey { get; }

    /// <summary>The permission the caller must hold to use this tool (a <c>Permissions</c> constant); null = none.</summary>
    string? RequiredPermission { get; }

    Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default);
}

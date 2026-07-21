using System.Text.Json;
using Korkut.Application.Runtime;

namespace Korkut.Agent.Tools;

/// <summary>
/// Collects the registered <see cref="IAgentTool"/>s and exposes only those whose key requirement
/// is satisfied, so a tool missing its API key is silently hidden rather than failing the turn.
/// Dispatches an invocation by name; an unknown or unavailable name returns an error result the
/// model can recover from.
/// </summary>
public sealed class ToolCatalog(IEnumerable<IAgentTool> tools, IEnvironment env) : IToolCatalog
{
    private readonly IReadOnlyList<IAgentTool> _available =
        tools.Where(t => IsAvailable(t.RequiresKey, env)).ToList();

    /// <summary>A tool with no key requirement is always available; otherwise its key must be set.</summary>
    private static bool IsAvailable(string? requiresKey, IEnvironment env)
        => requiresKey is null || !string.IsNullOrWhiteSpace(env.Get(requiresKey));

    public IReadOnlyList<ToolSpec> AvailableSpecs() => _available.Select(t => t.Spec).ToList();

    public Task<ToolResult> InvokeAsync(string name, JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var tool = _available.FirstOrDefault(t => t.Spec.Name == name);
        return tool is null
            ? Task.FromResult(ToolResult.Error($"Bilinmeyen veya kullanılamayan araç: {name}"))
            : tool.InvokeAsync(arguments, context, ct);
    }
}

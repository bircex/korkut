using System.Text.Json;
using Korkut.Application.Runtime;

namespace Korkut.Agent.Tools;

/// <summary>
/// Collects the registered <see cref="IAgentTool"/>s and exposes only those a given caller may use:
/// configured (its API key present, if any) AND permitted (the caller holds its required permission).
/// So a tool missing its key, or one the user lacks the right for, is neither advertised to the model
/// nor executed. Dispatches an invocation by name; an unknown/unavailable name or a missing
/// permission returns an error result the model can recover from (and surface to the user).
/// </summary>
public sealed class ToolCatalog(IEnumerable<IAgentTool> tools, IEnvironment env) : IToolCatalog
{
    // Key-gating is per-environment (not per-user), so it is resolved once at construction.
    private readonly IReadOnlyList<IAgentTool> _configured =
        tools.Where(t => IsKeyPresent(t.RequiresKey, env)).ToList();

    /// <summary>A tool with no key requirement is always configured; otherwise its key must be set.</summary>
    private static bool IsKeyPresent(string? requiresKey, IEnvironment env)
        => requiresKey is null || !string.IsNullOrWhiteSpace(env.Get(requiresKey));

    public IReadOnlyList<ToolSpec> AvailableSpecs(ToolContext context)
        => _configured.Where(t => context.Can(t.RequiredPermission)).Select(t => t.Spec).ToList();

    public Task<ToolResult> InvokeAsync(string name, JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var tool = _configured.FirstOrDefault(t => t.Spec.Name == name);
        if (tool is null)
        {
            return Task.FromResult(ToolResult.Error($"Bilinmeyen veya kullanılamayan araç: {name}"));
        }

        // Permission (RBAC) enforced at invocation too — defense in depth, even if the tool was hidden.
        return context.Can(tool.RequiredPermission)
            ? tool.InvokeAsync(arguments, context, ct)
            : Task.FromResult(ToolResult.Error($"Bu işlem için yetkiniz yok: {name}"));
    }
}

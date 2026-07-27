using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Runtime;

namespace Korkut.Infrastructure.Agent.Tools;

/// <summary>
/// The registered tools, offered and dispatched for one caller. A tool is available only when it is
/// both CONFIGURED (its <see cref="IAgentTool.RequiresKey"/> environment variable is present, if it
/// declares one) and PERMITTED (the caller holds <see cref="IAgentTool.RequiredPermission"/>). An
/// unavailable tool is neither advertised to the model nor invocable — asking for one by name is
/// refused the same way a missing tool is, so RBAC cannot be bypassed by guessing a name.
/// </summary>
public sealed class ToolCatalog(IEnumerable<IAgentTool> tools, IEnvironment environment) : IToolCatalog
{
    private readonly IReadOnlyDictionary<string, IAgentTool> _byName =
        tools.ToDictionary(t => t.Spec.Name, StringComparer.Ordinal);

    public IReadOnlyList<ToolSpec> AvailableSpecs(AgentToolContext context)
        => _byName.Values.Where(t => IsAvailable(t, context)).Select(t => t.Spec).ToList();

    public async Task<ToolResult> InvokeAsync(
        string name, JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!_byName.TryGetValue(name, out var tool) || !IsAvailable(tool, context))
        {
            return ToolResult.Error($"'{name}' adlı bir araç yok veya kullanma yetkiniz yok.");
        }

        return await tool.InvokeAsync(arguments, context, ct);
    }

    private bool IsAvailable(IAgentTool tool, AgentToolContext context)
        => (tool.RequiresKey is null || !string.IsNullOrWhiteSpace(environment.Get(tool.RequiresKey)))
            && context.Can(tool.RequiredPermission);
}

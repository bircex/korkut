using System.Text.Json;
using Korkut.Application.Agent.Models;

namespace Korkut.Application.Agent.Tools;

/// <summary>
/// The single surface the tool-calling loop uses: the tools available to THIS caller (to advertise
/// to the model) and a name-based dispatch to invoke one. Availability is filtered by each tool's
/// key requirement AND the caller's permissions, so a tool that is unconfigured or not permitted is
/// neither advertised nor invocable.
/// </summary>
public interface IToolCatalog
{
    IReadOnlyList<ToolSpec> AvailableSpecs(AgentToolContext context);

    Task<ToolResult> InvokeAsync(string name, JsonElement arguments, AgentToolContext context, CancellationToken ct = default);
}

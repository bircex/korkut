using System.Text.Json;

namespace Korkut.Agent.Tools;

/// <summary>
/// The single surface the tool-calling loop uses: the currently-available tool specs (to advertise
/// to the model) and a name-based dispatch to invoke one. Availability is filtered by each tool's
/// key requirement, so an unconfigured tool is neither advertised nor invocable.
/// </summary>
public interface IToolCatalog
{
    /// <summary>The tools offered to the model for THIS caller: configured (key present) AND permitted.</summary>
    IReadOnlyList<ToolSpec> AvailableSpecs(ToolContext context);

    Task<ToolResult> InvokeAsync(string name, JsonElement arguments, ToolContext context, CancellationToken ct = default);
}

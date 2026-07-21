using System.Text.Json;
using Korkut.Application.Features.OfficeCore;

namespace Korkut.Agent.Tools.Office;

/// <summary>Lists the office's clients (müvekkiller).</summary>
public sealed class ListClientsTool(IOfficeScope scope, IClientService clients) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse("""{"type":"object","properties":{}}""");

    public ToolSpec Spec => new("list_clients", "Bürodaki müvekkilleri listeler.", Schema);

    public string? RequiresKey => null;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var list = await scope.RunAsync(context.OfficeId, c => clients.ListAsync(context.OfficeId, c), ct);
        return ToolResult.Ok(ToolArgs.Result(list.Select(x => new { x.Id, x.Name, x.Kind, x.Email, x.Phone })));
    }
}

using System.Text.Json;
using Korkut.Application.Features.Correspondence;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Lists the correspondence/document register (evrak defteri), optionally by direction.</summary>
public sealed class ListEvrakTool(IOfficeScope scope, ICorrespondenceService correspondence) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """{"type":"object","properties":{"direction":{"type":"string","enum":["incoming","outgoing","internal"],"description":"Yön filtresi (opsiyonel)"}}}""");

    public ToolSpec Spec => new("list_evrak", "Evrak defterini (gelen/giden evrak) listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.DocumentsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var list = await scope.RunAsync(context.OfficeId,
            c => correspondence.ListAsync(context.OfficeId, ToolArgs.String(arguments, "direction"), c), ct);
        return ToolResult.Ok(ToolArgs.Result(list));
    }
}

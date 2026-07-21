using System.Text.Json;
using Korkut.Application.Features.OfficeOps;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Lists communication notes (iletişim notları), optionally for one matter.</summary>
public sealed class ListNotesTool(IOfficeScope scope, IOfficeOpsService ops) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """{"type":"object","properties":{"matter_id":{"type":"string","description":"Yalnızca bu dosyanın notları (opsiyonel)"}}}""");

    public ToolSpec Spec => new("list_notes", "İletişim/görüşme notlarını listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        Guid? matterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null;
        var list = await scope.RunAsync(context.OfficeId, c => ops.ListNotesAsync(context.OfficeId, matterId, c), ct);
        return ToolResult.Ok(ToolArgs.Result(list));
    }
}

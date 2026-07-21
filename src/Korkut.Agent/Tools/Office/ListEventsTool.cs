using System.Text.Json;
using Korkut.Application.Features.Calendar;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Lists calendar entries (events + hearings + deadlines) in a range.</summary>
public sealed class ListEventsTool(IOfficeScope scope, ICalendarService calendar) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "start": { "type": "string", "description": "Aralık başı (ISO tarih)" },
            "end": { "type": "string", "description": "Aralık sonu (ISO tarih)" },
            "category": { "type": "string", "enum": ["appointment", "hearing", "deadline", "reminder"] },
            "matter_id": { "type": "string" }
          }
        }
        """);

    public ToolSpec Spec => new("list_events", "Takvim kayıtlarını (etkinlik/duruşma/süre) bir aralıkta listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        Guid? matterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null;
        var list = await scope.RunAsync(context.OfficeId,
            c => calendar.ListEntriesAsync(context.OfficeId, ToolArgs.DateTime(arguments, "start"), ToolArgs.DateTime(arguments, "end"), ToolArgs.String(arguments, "category"), matterId, c), ct);
        return ToolResult.Ok(ToolArgs.Result(list));
    }
}

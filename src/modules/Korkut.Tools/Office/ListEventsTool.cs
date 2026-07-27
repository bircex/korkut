using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Contract.Calendar;

namespace Korkut.Tools.Office;

/// <summary>Lists calendar entries, optionally within a date range or for a matter.</summary>
public sealed class ListEventsTool(ICalendarService calendar) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "start": { "type": "string", "format": "date-time", "description": "Aralık başı (opsiyonel)" },
            "end": { "type": "string", "format": "date-time", "description": "Aralık sonu (opsiyonel)" },
            "matter_id": { "type": "string", "description": "Dosya (opsiyonel)" }
          }
        }
        """);

    public ToolSpec Spec => new("list_events", "Takvim kayıtlarını listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var entries = await calendar.ListEntriesAsync(
            ToolArgs.DateTime(arguments, "start"),
            ToolArgs.DateTime(arguments, "end"),
            category: null,
            ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null,
            ct);
        return entries.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(entries.Value!))
            : ToolResult.Error(entries.Error!.Detail);
    }
}

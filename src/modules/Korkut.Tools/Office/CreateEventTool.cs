using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Calendar;
using Korkut.Tools.Tools;

namespace Korkut.Tools.Office;

/// <summary>Takvime bir randevu/etkinlik ekler.</summary>
public sealed class CreateEventTool(ICalendarService calendar) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "Etkinlik başlığı" },
            "start_at": { "type": "string", "format": "date-time", "description": "Başlangıç (ISO 8601)" },
            "end_at": { "type": "string", "format": "date-time", "description": "Bitiş (opsiyonel)" },
            "matter_id": { "type": "string", "description": "Bağlanacak dosya (opsiyonel)" }
          },
          "required": ["title", "start_at"]
        }
        """);

    public ToolSpec Spec => new("create_event", "Takvime bir randevu/etkinlik ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var title = ToolArgs.String(arguments, "title");
        var startAt = ToolArgs.DateTime(arguments, "start_at");
        if (string.IsNullOrWhiteSpace(title) || startAt is not { } start)
        {
            return ToolResult.Error("'title' ve 'start_at' gereklidir.");
        }

        var request = new CreateCalendarEventRequest
        {
            Title = title.Trim(),
            StartAt = start,
            EndAt = ToolArgs.DateTime(arguments, "end_at"),
            MatterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null,
        };
        var result = await calendar.CreateAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToolResult.Error(result.Error!.Detail);
        }

        var entry = result.Value!;
        return ToolResult.Ok(ToolArgs.Result(new { entry.Id, entry.Title, entry.StartAt }));
    }
}

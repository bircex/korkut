using System.Text.Json;
using Korkut.Application.Features.Calendar;
using Korkut.Application.Security;
using Korkut.Domain.Calendar;

namespace Korkut.Agent.Tools.Office;

/// <summary>Creates a calendar event (takvim etkinliği) for the office.</summary>
public sealed class CreateEventTool(IOfficeScope scope, ICalendarService calendar) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string" },
            "start_at": { "type": "string", "description": "Başlangıç (ISO tarih-saat)" },
            "category": { "type": "string", "enum": ["appointment", "hearing", "deadline", "reminder"], "description": "Varsayılan appointment" },
            "type": { "type": "string" },
            "end_at": { "type": "string" },
            "all_day": { "type": "boolean" },
            "matter_id": { "type": "string" },
            "client_id": { "type": "string" },
            "location": { "type": "string" },
            "notes": { "type": "string" },
            "remind_before_min": { "type": "integer" }
          },
          "required": ["title", "start_at"]
        }
        """);

    public ToolSpec Spec => new("create_event", "Büro takvimine yeni bir etkinlik ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var title = ToolArgs.String(arguments, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return ToolResult.Error("'title' gereklidir.");
        }

        if (ToolArgs.DateTime(arguments, "start_at") is not { } startAt)
        {
            return ToolResult.Error("'start_at' geçerli bir tarih-saat olmalıdır.");
        }

        var draft = new CalendarEventDraft(
            Category: ToolArgs.String(arguments, "category") ?? "appointment",
            Title: title.Trim(),
            StartAt: startAt,
            EndAt: ToolArgs.DateTime(arguments, "end_at"),
            AllDay: ToolArgs.Bool(arguments, "all_day"),
            Type: ToolArgs.String(arguments, "type"),
            MatterId: ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null,
            ClientId: ToolArgs.TryGuid(arguments, "client_id", out var cid) ? cid : null,
            AssigneeId: null,
            Location: ToolArgs.String(arguments, "location"),
            Notes: ToolArgs.String(arguments, "notes"),
            Rrule: null,
            RemindBeforeMin: ToolArgs.Int(arguments, "remind_before_min"));

        var entry = await scope.RunAsync(context.OfficeId, c => calendar.CreateEventAsync(context.OfficeId, draft, c), ct);
        return ToolResult.Ok(ToolArgs.Result(entry));
    }
}

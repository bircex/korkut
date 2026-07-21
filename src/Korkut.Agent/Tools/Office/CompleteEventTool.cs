using System.Text.Json;
using Korkut.Application.Features.Calendar;
using Korkut.Application.Security;
using Korkut.Domain.Calendar;

namespace Korkut.Agent.Tools.Office;

/// <summary>Marks a calendar event as done.</summary>
public sealed class CompleteEventTool(IOfficeScope scope, ICalendarService calendar) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """{"type":"object","properties":{"event_id":{"type":"string"}},"required":["event_id"]}""");

    public ToolSpec Spec => new("complete_event", "Bir takvim etkinliğini tamamlandı olarak işaretler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryGuid(arguments, "event_id", out var id))
        {
            return ToolResult.Error("'event_id' geçerli bir kimlik olmalıdır.");
        }

        var patch = new CalendarEventPatch(null, null, null, null, null, null, null, null, null, Status: "done", null, null, null, null);
        var entry = await scope.RunAsync(context.OfficeId, c => calendar.UpdateEventAsync(context.OfficeId, id, patch, c), ct);
        return entry is null ? ToolResult.Ok("Etkinlik bulunamadı.") : ToolResult.Ok(ToolArgs.Result(entry));
    }
}

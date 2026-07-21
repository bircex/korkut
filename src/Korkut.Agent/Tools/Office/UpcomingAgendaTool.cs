using System.Text.Json;
using Korkut.Application.Features.Calendar;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Upcoming agenda — the office's hearings and open deadlines within N days.</summary>
public sealed class UpcomingAgendaTool(IOfficeScope scope, ICalendarService calendar) : IAgentTool
{
    private const int DefaultDays = 14;

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """{"type":"object","properties":{"days":{"type":"integer","description":"Kaç günlük ufuk (varsayılan 14)"}}}""");

    public ToolSpec Spec => new("upcoming_agenda", "Yaklaşan duruşma ve açık süreleri (ajanda) getirir.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var days = ToolArgs.Int(arguments, "days") ?? DefaultDays;
        var agenda = await scope.RunAsync(context.OfficeId, c => calendar.AgendaAsync(context.OfficeId, days, c), ct);
        return ToolResult.Ok(ToolArgs.Result(agenda));
    }
}

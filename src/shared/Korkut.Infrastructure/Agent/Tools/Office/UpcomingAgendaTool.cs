using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Contract.Calendar;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>The upcoming agenda: hearings and deadlines over the next N days.</summary>
public sealed class UpcomingAgendaTool(ICalendarService calendar) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "days": { "type": "integer", "description": "Kaç günlük pencere (varsayılan 14)" }
          }
        }
        """);

    public ToolSpec Spec => new("upcoming_agenda", "Önümüzdeki günlerin duruşma ve sürelerini getirir.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var days = Math.Clamp(ToolArgs.Int(arguments, "days") ?? 14, 1, 365);
        var agenda = await calendar.AgendaAsync(days, ct);
        return agenda.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(agenda.Value!))
            : ToolResult.Error(agenda.Error!.Detail);
    }
}

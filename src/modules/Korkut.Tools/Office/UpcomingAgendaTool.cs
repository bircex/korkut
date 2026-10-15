using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Calendar;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

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

    public string? RequiredPermission => Permissions.AgendaRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var days = Math.Clamp(ToolArgs.Int(arguments, "days") ?? 14, 1, 365);
        var agenda = await calendar.AgendaAsync(days, ct);
        return agenda.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(context.MatterId is { } bound
                ? agenda.Value! with { Items = agenda.Value!.Items.Where(i => i.MatterId == bound).ToList() }
                : agenda.Value!))
            : ToolResult.Error(agenda.Error!.Detail);
    }
}

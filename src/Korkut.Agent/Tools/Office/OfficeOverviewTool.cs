using System.Text.Json;
using Korkut.Application.Features.Reports;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Returns an office overview (dashboard) — matters/hearings/deadlines/finance at a glance.</summary>
public sealed class OfficeOverviewTool(IOfficeScope scope, IReportsService reports) : IAgentTool
{
    private const int DefaultDays = 14;

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """{"type":"object","properties":{"days":{"type":"integer","description":"Kaç günlük ufuk (varsayılan 14)"}}}""");

    public ToolSpec Spec => new("office_overview", "Büronun genel görünümünü (dosya/duruşma/süre/mali özet) getirir.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var days = ToolArgs.Int(arguments, "days") ?? DefaultDays;
        var overview = await scope.RunAsync(context.OfficeId, c => reports.OverviewAsync(context.OfficeId, days, c), ct);
        return ToolResult.Ok(ToolArgs.Result(overview));
    }
}

using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Reports;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

/// <summary>The office dashboard roll-up (matters/clients/finance/tasks/agenda/evrak).</summary>
public sealed class OfficeOverviewTool(IReportsService reports) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "days": { "type": "integer", "description": "Ajanda penceresi (gün, varsayılan 14)" }
          }
        }
        """);

    public ToolSpec Spec => new("office_overview", "Büronun genel durum özetini verir.", Schema);

    public string? RequiresKey => null;

    // Surfaces money, like the reports endpoint → billing.read (both built-in roles hold it).
    public string? RequiredPermission => Permissions.ReportsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var overview = await reports.OverviewAsync(ToolArgs.Int(arguments, "days"), ct);
        return ToolResult.Ok(ToolArgs.Result(overview));
    }
}

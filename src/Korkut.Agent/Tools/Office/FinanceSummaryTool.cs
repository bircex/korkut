using System.Text.Json;
using Korkut.Application.Features.Finance;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Returns the office's finance totals (gelir/gider/bakiye özeti).</summary>
public sealed class FinanceSummaryTool(IOfficeScope scope, IFinanceService finance) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse("""{"type":"object","properties":{}}""");

    public ToolSpec Spec => new("finance_summary", "Büronun mali özetini (fatura/masraf toplamları) getirir.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.BillingRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var totals = await scope.RunAsync(context.OfficeId, c => finance.SummaryAsync(context.OfficeId, c), ct);
        return ToolResult.Ok(ToolArgs.Result(totals));
    }
}

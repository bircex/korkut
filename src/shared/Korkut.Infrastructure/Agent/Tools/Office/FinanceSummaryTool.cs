using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Finance;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>Büronun mali özetini (kesilen/tahsil/bakiye/masraf) verir.</summary>
public sealed class FinanceSummaryTool(IFinanceService finance) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        { "type": "object", "properties": {} }
        """);

    public ToolSpec Spec => new("finance_summary", "Büronun mali özetini (kesilen/tahsil/bakiye/masraf) verir.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.BillingRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var result = await finance.GetSummaryAsync(ct);
        return result.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(result.Value!))
            : ToolResult.Error(result.Error!.Detail);
    }
}

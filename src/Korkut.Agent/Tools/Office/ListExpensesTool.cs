using System.Text.Json;
using Korkut.Application.Features.Finance;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Lists the office's expenses (masraflar).</summary>
public sealed class ListExpensesTool(IOfficeScope scope, IFinanceService finance) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse("""{"type":"object","properties":{}}""");

    public ToolSpec Spec => new("list_expenses", "Bürodaki masrafları listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.BillingRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var list = await scope.RunAsync(context.OfficeId, c => finance.ListExpensesAsync(context.OfficeId, c), ct);
        return ToolResult.Ok(ToolArgs.Result(list));
    }
}

using System.Text.Json;
using Korkut.Application.Features.Finance;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Lists the office's invoices (faturalar).</summary>
public sealed class ListInvoicesTool(IOfficeScope scope, IFinanceService finance) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse("""{"type":"object","properties":{}}""");

    public ToolSpec Spec => new("list_invoices", "Bürodaki faturaları listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.BillingRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var list = await scope.RunAsync(context.OfficeId, c => finance.ListInvoicesAsync(context.OfficeId, c), ct);
        return ToolResult.Ok(ToolArgs.Result(list));
    }
}

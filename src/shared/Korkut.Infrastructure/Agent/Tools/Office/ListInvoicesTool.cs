using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Invoices;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>Faturaları listeler.</summary>
public sealed class ListInvoicesTool(IInvoiceService invoices) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        { "type": "object", "properties": {} }
        """);

    public ToolSpec Spec => new("list_invoices", "Faturaları listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.BillingRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var result = await invoices.ListAsync(ct);
        return result.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(result.Value!))
            : ToolResult.Error(result.Error!.Detail);
    }
}

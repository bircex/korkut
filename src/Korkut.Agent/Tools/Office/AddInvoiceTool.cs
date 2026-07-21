using System.Text.Json;
using Korkut.Application.Features.Finance;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Creates an invoice (fatura) for the office.</summary>
public sealed class AddInvoiceTool(IOfficeScope scope, IFinanceService finance) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "amount": { "type": "number", "description": "Tutar" },
            "vat_rate": { "type": "number", "description": "KDV oranı (varsayılan 20)" },
            "client_id": { "type": "string" },
            "matter_id": { "type": "string" },
            "description": { "type": "string" },
            "status": { "type": "string", "enum": ["draft", "issued", "paid", "cancelled"], "description": "Varsayılan draft" }
          },
          "required": ["amount"]
        }
        """);

    public ToolSpec Spec => new("add_invoice", "Büroya yeni bir fatura ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.BillingWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("amount", out var a) || !a.TryGetDecimal(out var amount))
        {
            return ToolResult.Error("'amount' sayısal ve gereklidir.");
        }

        var vat = arguments.TryGetProperty("vat_rate", out var v) && v.TryGetDecimal(out var vr) ? vr : 20m;
        Guid? clientId = ToolArgs.TryGuid(arguments, "client_id", out var cid) ? cid : null;
        Guid? matterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null;
        var status = ToolArgs.String(arguments, "status") ?? "draft";

        var invoice = await scope.RunAsync(context.OfficeId,
            c => finance.CreateInvoiceAsync(context.OfficeId, amount, vat, clientId, matterId, ToolArgs.String(arguments, "description"), status, issuedAt: null, c), ct);
        return ToolResult.Ok(ToolArgs.Result(invoice));
    }
}

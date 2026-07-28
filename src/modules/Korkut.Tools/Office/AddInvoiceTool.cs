using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Finance;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

/// <summary>Büroya yeni bir fatura ekler.</summary>
public sealed class AddInvoiceTool(IFinanceService finance) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "amount": { "type": "number", "description": "Tutar (KDV hariç)" },
            "client_id": { "type": "string", "description": "Müvekkil (opsiyonel)" },
            "matter_id": { "type": "string", "description": "Dosya (opsiyonel)" },
            "description": { "type": "string", "description": "Açıklama (opsiyonel)" }
          },
          "required": ["amount"]
        }
        """);

    public ToolSpec Spec => new("add_invoice", "Büroya yeni bir fatura ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.BillingWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("amount", out var amountEl) || !amountEl.TryGetDecimal(out var amount))
        {
            return ToolResult.Error("'amount' gereklidir.");
        }

        var request = new CreateInvoiceRequest
        {
            Amount = amount,
            ClientId = ToolArgs.TryGuid(arguments, "client_id", out var cid) ? cid : null,
            MatterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null,
            Description = ToolArgs.String(arguments, "description"),
        };
        var result = await finance.CreateInvoiceAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToolResult.Error(result.Error!.Detail);
        }

        var invoice = result.Value!;
        return ToolResult.Ok(ToolArgs.Result(new { invoice.Id, invoice.Amount, invoice.Status }));
    }
}

using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Finance;
using Korkut.Tools.Tools;

namespace Korkut.Tools.Office;

/// <summary>Büroya yeni bir masraf ekler.</summary>
public sealed class AddExpenseTool(IFinanceService finance) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "amount": { "type": "number", "description": "Tutar" },
            "description": { "type": "string", "description": "Açıklama (opsiyonel)" },
            "matter_id": { "type": "string", "description": "Bağlanacak dosya (opsiyonel)" }
          },
          "required": ["amount"]
        }
        """);

    public ToolSpec Spec => new("add_expense", "Büroya yeni bir masraf ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.BillingWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("amount", out var amountEl) || !amountEl.TryGetDecimal(out var amount))
        {
            return ToolResult.Error("'amount' gereklidir.");
        }

        var request = new CreateExpenseRequest
        {
            Amount = amount,
            Description = ToolArgs.String(arguments, "description"),
            MatterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null,
        };
        var result = await finance.CreateExpenseAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToolResult.Error(result.Error!.Detail);
        }

        var expense = result.Value!;
        return ToolResult.Ok(ToolArgs.Result(new { expense.Id, expense.Amount }));
    }
}

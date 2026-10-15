using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Finance;
using Korkut.Tools.Core;

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

    public string? RequiredPermission => Permissions.FinanceWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryMatter(arguments, context, out var matter, out var refusal))
        {
            return refusal!;
        }

        if (!arguments.TryGetProperty("amount", out var amountEl) || !amountEl.TryGetDecimal(out var amount))
        {
            return ToolResult.Error("'amount' gereklidir.");
        }

        var request = new CreateExpenseRequest
        {
            Amount = amount,
            Description = ToolArgs.String(arguments, "description"),
            MatterId = matter,
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

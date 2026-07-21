using System.Text.Json;
using Korkut.Application.Features.Finance;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Records an expense (masraf) for the office.</summary>
public sealed class AddExpenseTool(IOfficeScope scope, IFinanceService finance) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "amount": { "type": "number", "description": "Tutar" },
            "kind": { "type": "string", "description": "Masraf türü" },
            "matter_id": { "type": "string" },
            "client_id": { "type": "string" },
            "description": { "type": "string" }
          },
          "required": ["amount"]
        }
        """);

    public ToolSpec Spec => new("add_expense", "Büroya yeni bir masraf ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.BillingWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("amount", out var a) || !a.TryGetDecimal(out var amount))
        {
            return ToolResult.Error("'amount' sayısal ve gereklidir.");
        }

        Guid? matterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null;
        Guid? clientId = ToolArgs.TryGuid(arguments, "client_id", out var cid) ? cid : null;

        var expense = await scope.RunAsync(context.OfficeId,
            c => finance.CreateExpenseAsync(context.OfficeId, amount, matterId, clientId, ToolArgs.String(arguments, "kind"), ToolArgs.String(arguments, "description"), spentAt: null, c), ct);
        return ToolResult.Ok(ToolArgs.Result(expense));
    }
}

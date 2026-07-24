using System.Text.Json;
using Korkut.Application.Expenses;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>Masrafları listeler.</summary>
public sealed class ListExpensesTool(IExpenseService expenses) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        { "type": "object", "properties": {} }
        """);

    public ToolSpec Spec => new("list_expenses", "Masrafları listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.BillingRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var result = await expenses.ListAsync(ct);
        return ToolResult.Ok(ToolArgs.Result(result));
    }
}

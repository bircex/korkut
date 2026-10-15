using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Deadlines;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

/// <summary>Süreleri listeler (opsiyonel dosyaya göre).</summary>
public sealed class ListDeadlinesTool(IDeadlineService deadlines) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        { "type": "object", "properties": {"matter_id": { "type": "string", "description": "Dosya (opsiyonel)" }} }
        """);

    public ToolSpec Spec => new("list_deadlines", "Süreleri listeler (opsiyonel dosyaya göre).", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.AgendaRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryMatter(arguments, context, out var matter, out var refusal))
        {
            return refusal!;
        }

        var result = await deadlines.ListAsync(matter, ct);
        return result.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(result.Value!))
            : ToolResult.Error(result.Error!.Detail);
    }
}

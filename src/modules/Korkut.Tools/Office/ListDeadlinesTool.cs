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
        var result = await deadlines.ListAsync(ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null, ct);
        return result.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(result.Value!))
            : ToolResult.Error(result.Error!.Detail);
    }
}

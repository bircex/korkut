using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Notes;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

/// <summary>Görüşme notlarını listeler (opsiyonel dosyaya göre).</summary>
public sealed class ListNotesTool(INoteService notes) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        { "type": "object", "properties": {"matter_id": { "type": "string", "description": "Dosya (opsiyonel)" }} }
        """);

    public ToolSpec Spec => new("list_notes", "Görüşme notlarını listeler (opsiyonel dosyaya göre).", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.NotesRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryMatter(arguments, context, out var matter, out var refusal))
        {
            return refusal!;
        }

        var result = await notes.ListAsync(matter, ct);
        return result.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(result.Value!))
            : ToolResult.Error(result.Error!.Detail);
    }
}

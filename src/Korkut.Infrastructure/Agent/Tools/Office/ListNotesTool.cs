using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Notes;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>Görüşme notlarını listeler (opsiyonel dosyaya göre).</summary>
public sealed class ListNotesTool(INoteService notes) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        { "type": "object", "properties": {"matter_id": { "type": "string", "description": "Dosya (opsiyonel)" }} }
        """);

    public ToolSpec Spec => new("list_notes", "Görüşme notlarını listeler (opsiyonel dosyaya göre).", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var result = await notes.ListAsync(ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null, ct);
        return ToolResult.Ok(ToolArgs.Result(result));
    }
}

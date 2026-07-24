using System.Text.Json;
using Korkut.Application.Correspondence;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>Evrak kayıtlarını listeler (opsiyonel yöne göre).</summary>
public sealed class ListEvrakTool(ICorrespondenceService correspondence) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        { "type": "object", "properties": {"direction": { "type": "string", "enum": ["incoming","outgoing"], "description": "Yön (opsiyonel)" }} }
        """);

    public ToolSpec Spec => new("list_evrak", "Evrak kayıtlarını listeler (opsiyonel yöne göre).", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var result = await correspondence.ListAsync(ToolArgs.String(arguments, "direction"), ct);
        return ToolResult.Ok(ToolArgs.Result(result));
    }
}

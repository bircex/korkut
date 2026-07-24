using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Correspondence;
using Korkut.Application.Correspondence.Requests;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>Evrak defterine bir kayıt ekler (gelen/giden).</summary>
public sealed class AddEvrakTool(ICorrespondenceService correspondence) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "direction": { "type": "string", "enum": ["incoming", "outgoing"], "description": "Yön" },
            "title": { "type": "string", "description": "Evrak konusu" },
            "matter_id": { "type": "string", "description": "Bağlanacak dosya (opsiyonel)" }
          },
          "required": ["direction", "title"]
        }
        """);

    public ToolSpec Spec => new("add_evrak", "Evrak defterine bir kayıt ekler (gelen/giden).", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var direction = ToolArgs.String(arguments, "direction");
        var title = ToolArgs.String(arguments, "title");
        if (string.IsNullOrWhiteSpace(direction) || string.IsNullOrWhiteSpace(title))
        {
            return ToolResult.Error("'direction' ve 'title' gereklidir.");
        }

        var request = new CreateCorrespondenceRequest
        {
            Direction = direction.Trim(),
            Title = title.Trim(),
            MatterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null,
        };
        var evrak = await correspondence.CreateAsync(request, ct);
        return ToolResult.Ok(ToolArgs.Result(new { evrak.Id, evrak.Direction, evrak.Title }));
    }
}

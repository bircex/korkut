using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Correspondence;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

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

    public string? RequiredPermission => Permissions.NotesWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryMatter(arguments, context, out var matter, out var refusal))
        {
            return refusal!;
        }

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
            MatterId = matter,
        };
        var result = await correspondence.CreateAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToolResult.Error(result.Error!.Detail);
        }

        var evrak = result.Value!;
        return ToolResult.Ok(ToolArgs.Result(new { evrak.Id, evrak.Direction, evrak.Title }));
    }
}

using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Notes;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

/// <summary>Büroya bir görüşme notu ekler.</summary>
public sealed class AddNoteTool(INoteService notes) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "body": { "type": "string", "description": "Not metni" },
            "matter_id": { "type": "string", "description": "Bağlanacak dosya (opsiyonel)" }
          },
          "required": ["body"]
        }
        """);

    public ToolSpec Spec => new("add_note", "Büroya bir görüşme notu ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var body = ToolArgs.String(arguments, "body");
        if (string.IsNullOrWhiteSpace(body))
        {
            return ToolResult.Error("'body' gereklidir.");
        }

        var request = new CreateNoteRequest
        {
            Body = body.Trim(),
            MatterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null,
        };
        var result = await notes.CreateAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToolResult.Error(result.Error!.Detail);
        }

        return ToolResult.Ok(ToolArgs.Result(new { result.Value!.Id }));
    }
}

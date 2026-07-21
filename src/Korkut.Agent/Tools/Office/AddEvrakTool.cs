using System.Text.Json;
using Korkut.Application.Features.Correspondence;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Records an entry in the correspondence/document register (evrak defteri).</summary>
public sealed class AddEvrakTool(IOfficeScope scope, ICorrespondenceService correspondence) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string" },
            "direction": { "type": "string", "enum": ["incoming", "outgoing", "internal"], "description": "Gelen/giden/iç (varsayılan incoming)" },
            "reference_no": { "type": "string" },
            "doc_date": { "type": "string", "description": "Evrak tarihi (YYYY-MM-DD)" },
            "matter_id": { "type": "string" },
            "notes": { "type": "string" }
          },
          "required": ["title"]
        }
        """);

    public ToolSpec Spec => new("add_evrak", "Evrak defterine gelen/giden bir evrak kaydı ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.DocumentsWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var title = ToolArgs.String(arguments, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return ToolResult.Error("'title' gereklidir.");
        }

        var direction = ToolArgs.String(arguments, "direction") ?? "incoming";
        Guid? matterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null;

        var item = await scope.RunAsync(context.OfficeId,
            c => correspondence.CreateAsync(context.OfficeId, direction, title.Trim(), ToolArgs.String(arguments, "reference_no"), ToolArgs.Date(arguments, "doc_date"), matterId, ToolArgs.String(arguments, "notes"), c), ct);
        return ToolResult.Ok(ToolArgs.Result(item));
    }
}

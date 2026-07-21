using System.Text.Json;
using Korkut.Application.Features.OfficeOps;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Records a communication note (görüşme/iletişim notu) for a matter or client.</summary>
public sealed class AddNoteTool(IOfficeScope scope, IOfficeOpsService ops) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "body": { "type": "string", "description": "Not metni" },
            "channel": { "type": "string", "description": "İletişim kanalı (telefon/e-posta/yüz yüze...) (varsayılan note)" },
            "matter_id": { "type": "string" },
            "client_id": { "type": "string" }
          },
          "required": ["body"]
        }
        """);

    public ToolSpec Spec => new("add_note", "Bir dosya veya müvekkil için görüşme/iletişim notu ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var body = ToolArgs.String(arguments, "body");
        if (string.IsNullOrWhiteSpace(body))
        {
            return ToolResult.Error("'body' gereklidir.");
        }

        Guid? matterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null;
        Guid? clientId = ToolArgs.TryGuid(arguments, "client_id", out var cid) ? cid : null;
        var channel = ToolArgs.String(arguments, "channel") ?? "note";

        var note = await scope.RunAsync(context.OfficeId,
            c => ops.CreateNoteAsync(context.OfficeId, body.Trim(), channel, matterId, clientId, occurredAt: null, c), ct);
        return ToolResult.Ok(ToolArgs.Result(note));
    }
}

using System.Text.Json;
using Korkut.Application.Features.OfficeCore;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Adds a hearing (duruşma) to a matter.</summary>
public sealed class AddHearingTool(IOfficeScope scope, IMatterService matters) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "matter_id": { "type": "string" },
            "scheduled_at": { "type": "string", "description": "Duruşma tarih-saati (ISO)" },
            "type": { "type": "string" },
            "location": { "type": "string" },
            "notes": { "type": "string" }
          },
          "required": ["matter_id", "scheduled_at"]
        }
        """);

    public ToolSpec Spec => new("add_hearing", "Bir dosyaya duruşma ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryGuid(arguments, "matter_id", out var matterId))
        {
            return ToolResult.Error("'matter_id' geçerli bir kimlik olmalıdır.");
        }

        if (ToolArgs.DateTime(arguments, "scheduled_at") is not { } scheduledAt)
        {
            return ToolResult.Error("'scheduled_at' geçerli bir tarih-saat olmalıdır.");
        }

        var hearing = await scope.RunAsync(context.OfficeId,
            c => matters.AddHearingAsync(context.OfficeId, matterId, scheduledAt, ToolArgs.String(arguments, "type"), ToolArgs.String(arguments, "location"), ToolArgs.String(arguments, "notes"), c), ct);
        return hearing is null ? ToolResult.Ok("Dosya bulunamadı.") : ToolResult.Ok(ToolArgs.Result(hearing));
    }
}

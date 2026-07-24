using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Hearings;
using Korkut.Application.Hearings.Requests;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>Adds a hearing (duruşma) to a matter.</summary>
public sealed class AddHearingTool(IHearingService hearings) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "matter_id": { "type": "string", "description": "Duruşmanın ekleneceği dosya kimliği" },
            "date": { "type": "string", "format": "date", "description": "Duruşma günü (YYYY-MM-DD)" },
            "time": { "type": "string", "description": "Saat (HH:mm, opsiyonel)" },
            "location": { "type": "string", "description": "Yer (opsiyonel)" }
          },
          "required": ["matter_id", "date"]
        }
        """);

    public ToolSpec Spec => new("add_hearing", "Bir dosyaya duruşma ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryGuid(arguments, "matter_id", out var matterId))
        {
            return ToolResult.Error("'matter_id' gereklidir.");
        }

        if (ToolArgs.Date(arguments, "date") is not { } date)
        {
            return ToolResult.Error("'date' gereklidir.");
        }

        var request = new CreateHearingRequest
        {
            Date = date,
            Time = TimeOnly.TryParse(ToolArgs.String(arguments, "time"), out var t) ? t : null,
            Location = ToolArgs.String(arguments, "location"),
        };
        var hearing = await hearings.AddAsync(matterId, request, ct);
        return ToolResult.Ok(ToolArgs.Result(new { hearing.Id, hearing.ScheduledAt, hearing.Location }));
    }
}

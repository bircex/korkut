using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Tasks;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

/// <summary>Takvime bir randevu ekler (ajanda kaydı, "randevu" kategorisi).</summary>
public sealed class CreateEventTool(ITaskService tasks) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "Randevu başlığı" },
            "start_at": { "type": "string", "format": "date-time", "description": "Başlangıç (ISO 8601)" },
            "end_at": { "type": "string", "format": "date-time", "description": "Bitiş (opsiyonel)" },
            "matter_id": { "type": "string", "description": "Bağlanacak dosya (opsiyonel)" }
          },
          "required": ["title", "start_at"]
        }
        """);

    public ToolSpec Spec => new("create_event", "Takvime bir randevu ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.AgendaWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var title = ToolArgs.String(arguments, "title");
        var startAt = ToolArgs.DateTime(arguments, "start_at");
        if (string.IsNullOrWhiteSpace(title) || startAt is not { } start)
        {
            return ToolResult.Error("'title' ve 'start_at' gereklidir.");
        }

        var request = new CreateTaskRequest
        {
            Category = "randevu",
            Title = title.Trim(),
            DueDate = start,
            EndAt = ToolArgs.DateTime(arguments, "end_at"),
            AllDay = false,
            MatterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null,
        };
        var result = await tasks.CreateAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToolResult.Error(result.Error!.Detail);
        }

        var entry = result.Value!;
        return ToolResult.Ok(ToolArgs.Result(new { entry.Id, entry.Title, StartAt = entry.DueDate }));
    }
}

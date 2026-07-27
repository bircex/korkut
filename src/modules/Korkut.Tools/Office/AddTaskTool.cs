using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Contract.Tasks;

namespace Korkut.Tools.Office;

/// <summary>Adds a task (görev) to the office, optionally tied to a matter and due date.</summary>
public sealed class AddTaskTool(ITaskService tasks) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "Görev başlığı" },
            "matter_id": { "type": "string", "description": "Bağlanacak dosya kimliği (opsiyonel)" },
            "due_at": { "type": "string", "format": "date-time", "description": "Son tarih (ISO 8601, opsiyonel)" }
          },
          "required": ["title"]
        }
        """);

    public ToolSpec Spec => new("add_task", "Büroya yeni bir görev ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var title = ToolArgs.String(arguments, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return ToolResult.Error("'title' gereklidir.");
        }

        var request = new CreateTaskRequest
        {
            Title = title.Trim(),
            MatterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null,
            DueAt = ToolArgs.DateTime(arguments, "due_at"),
        };
        var result = await tasks.CreateAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToolResult.Error(result.Error!.Detail);
        }

        var task = result.Value!;
        return ToolResult.Ok(ToolArgs.Result(new { task.Id, task.Title, task.Status, task.DueAt }));
    }
}

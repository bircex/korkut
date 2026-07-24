using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Security;
using Korkut.Application.Tasks;
using Korkut.Application.Tasks.Requests;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>Marks a task (görev) done.</summary>
public sealed class CompleteTaskTool(ITaskService tasks) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "task_id": { "type": "string", "description": "Tamamlanacak görev kimliği" }
          },
          "required": ["task_id"]
        }
        """);

    public ToolSpec Spec => new("complete_task", "Bir görevi tamamlandı olarak işaretler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryGuid(arguments, "task_id", out var taskId))
        {
            return ToolResult.Error("'task_id' gereklidir.");
        }

        var task = await tasks.SetStatusAsync(taskId, new UpdateTaskStatusRequest { Status = "done" }, ct);
        return ToolResult.Ok(ToolArgs.Result(new { task.Id, task.Status, task.CompletedAt }));
    }
}

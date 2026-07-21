using System.Text.Json;
using Korkut.Application.Features.OfficeOps;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Marks a work task as done.</summary>
public sealed class CompleteTaskTool(IOfficeScope scope, IOfficeOpsService ops) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """{"type":"object","properties":{"task_id":{"type":"string"}},"required":["task_id"]}""");

    public ToolSpec Spec => new("complete_task", "Bir görevi tamamlandı olarak işaretler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryGuid(arguments, "task_id", out var id))
        {
            return ToolResult.Error("'task_id' geçerli bir kimlik olmalıdır.");
        }

        var task = await scope.RunAsync(context.OfficeId, c => ops.SetTaskStatusAsync(context.OfficeId, id, "done", c), ct);
        return task is null ? ToolResult.Ok("Görev bulunamadı.") : ToolResult.Ok(ToolArgs.Result(task));
    }
}

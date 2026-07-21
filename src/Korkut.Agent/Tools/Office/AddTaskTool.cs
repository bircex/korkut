using System.Text.Json;
using Korkut.Application.Features.OfficeOps;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Creates a work task (görev) for the office.</summary>
public sealed class AddTaskTool(IOfficeScope scope, IOfficeOpsService ops) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string" },
            "matter_id": { "type": "string" },
            "assignee_id": { "type": "string" },
            "due_at": { "type": "string", "description": "YYYY-MM-DD veya ISO tarih" }
          },
          "required": ["title"]
        }
        """);

    public ToolSpec Spec => new("add_task", "Büroya yeni bir görev ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var title = ToolArgs.String(arguments, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return ToolResult.Error("'title' gereklidir.");
        }

        Guid? matterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null;
        Guid? assigneeId = ToolArgs.TryGuid(arguments, "assignee_id", out var aid) ? aid : null;
        var task = await scope.RunAsync(context.OfficeId,
            c => ops.CreateTaskAsync(context.OfficeId, title.Trim(), matterId, assigneeId, ToolArgs.DateTime(arguments, "due_at"), c), ct);
        return ToolResult.Ok(ToolArgs.Result(task));
    }
}

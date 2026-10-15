using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Deadlines;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

/// <summary>Büroya yeni bir süre (deadline) ekler.</summary>
public sealed class AddDeadlineTool(IDeadlineService deadlines) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "Süre başlığı" },
            "due_date": { "type": "string", "format": "date", "description": "Son gün (YYYY-MM-DD)" },
            "matter_id": { "type": "string", "description": "Bağlanacak dosya (opsiyonel)" }
          },
          "required": ["title", "due_date"]
        }
        """);

    public ToolSpec Spec => new("add_deadline", "Büroya yeni bir süre (deadline) ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.AgendaWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var title = ToolArgs.String(arguments, "title");
        var dueDate = ToolArgs.Date(arguments, "due_date");
        if (string.IsNullOrWhiteSpace(title) || dueDate is not { } due)
        {
            return ToolResult.Error("'title' ve 'due_date' gereklidir.");
        }

        var request = new CreateDeadlineRequest
        {
            Title = title.Trim(),
            DueDate = due,
            MatterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null,
        };
        var result = await deadlines.CreateAsync(request, ct);
        if (!result.IsSuccess)
        {
            return ToolResult.Error(result.Error!.Detail);
        }

        var deadline = result.Value!;
        return ToolResult.Ok(ToolArgs.Result(new { deadline.Id, deadline.Title, deadline.DueDate }));
    }
}

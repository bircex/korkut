using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Deadlines;
using Korkut.Application.Deadlines.Requests;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Agent.Tools.Office;

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

    public string? RequiredPermission => Permissions.MattersWrite;

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
        var deadline = await deadlines.CreateAsync(request, ct);
        return ToolResult.Ok(ToolArgs.Result(new { deadline.Id, deadline.Title, deadline.DueDate }));
    }
}

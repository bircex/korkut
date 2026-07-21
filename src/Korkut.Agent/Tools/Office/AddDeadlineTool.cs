using System.Text.Json;
using Korkut.Application.Features.OfficeOps;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Adds a legal deadline (süre/tarih) to the office calendar.</summary>
public sealed class AddDeadlineTool(IOfficeScope scope, IOfficeOpsService ops) : IAgentTool
{
    private const string DefaultTimeZone = "Europe/Istanbul";
    
    private const int DefaultReminderDays = 3;

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string" },
            "due_date": { "type": "string", "description": "YYYY-MM-DD" },
            "kind": { "type": "string", "description": "Süre türü (varsayılan deadline)" },
            "matter_id": { "type": "string" },
            "reminder_days_before": { "type": "integer", "description": "Kaç gün önce hatırlat (varsayılan 3)" }
          },
          "required": ["title", "due_date"]
        }
        """);

    public ToolSpec Spec => new("add_deadline", "Büroya yeni bir yasal süre/tarih ekler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var title = ToolArgs.String(arguments, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return ToolResult.Error("'title' gereklidir.");
        }

        if (ToolArgs.Date(arguments, "due_date") is not { } dueDate)
        {
            return ToolResult.Error("'due_date' YYYY-MM-DD biçiminde olmalıdır.");
        }

        Guid? matterId = ToolArgs.TryGuid(arguments, "matter_id", out var mid) ? mid : null;
        var kind = ToolArgs.String(arguments, "kind") ?? "deadline";
        var reminder = ToolArgs.Int(arguments, "reminder_days_before") ?? DefaultReminderDays;

        var deadline = await scope.RunAsync(context.OfficeId,
            c => ops.CreateDeadlineAsync(context.OfficeId, title.Trim(), dueDate, kind, matterId, DefaultTimeZone, reminder, c), ct);
        return ToolResult.Ok(ToolArgs.Result(deadline));
    }
}

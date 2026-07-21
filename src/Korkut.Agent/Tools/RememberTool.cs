using System.Text.Json;
using Korkut.Application.Features.Settings;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools;

/// <summary>
/// Proposes a durable office memory (a fact / preference the assistant should remember). Saved as
/// PENDING for an owner to approve — never silently applied — matching the office-memory review model.
/// </summary>
public sealed class RememberTool(IOfficeScope scope, IOfficeSettingsService settings) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "text": { "type": "string", "description": "Hatırlanacak not" },
            "kind": { "type": "string", "enum": ["note", "fact", "preference"], "description": "Varsayılan preference" }
          },
          "required": ["text"]
        }
        """);

    public ToolSpec Spec => new(
        "remember",
        "Büro için kalıcı bir notu/tercihi hatırlamayı önerir (onay bekleyen olarak kaydedilir).",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var text = ToolArgs.String(arguments, "text");
        if (string.IsNullOrWhiteSpace(text))
        {
            return ToolResult.Error("'text' gereklidir.");
        }

        var kind = ToolArgs.String(arguments, "kind") ?? "preference";
        // active: false → the item is pending until an owner approves it.
        var item = await scope.RunAsync(context.OfficeId,
            c => settings.AddMemoryAsync(context.OfficeId, text.Trim(), kind, context.UserId, active: false, c), ct);
        return ToolResult.Ok(ToolArgs.Result(new { remembered = true, id = item.Id, status = "pending" }));
    }
}

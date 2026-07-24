using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Security;
using Korkut.Application.Settings;
using Korkut.Application.Settings.Requests;

namespace Korkut.Infrastructure.Agent.Tools;

/// <summary>
/// Proposes a durable office memory (a fact or preference the assistant should remember). It goes
/// through the office-memory service, which decides — from the caller's permissions — whether it
/// lands active or pending; a member's memory waits for an owner's approval, never silently applied.
/// </summary>
public sealed class RememberTool(IMemoryService memory) : IAgentTool
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
        "Büro için kalıcı bir notu/tercihi hatırlamayı önerir (üye ise onay bekleyen olarak kaydedilir).",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var text = ToolArgs.String(arguments, "text");
        if (string.IsNullOrWhiteSpace(text))
        {
            return ToolResult.Error("'text' gereklidir.");
        }

        var item = await memory.AddAsync(
            new CreateMemoryRequest { Text = text.Trim(), Kind = ToolArgs.String(arguments, "kind") ?? "preference" }, ct);
        return ToolResult.Ok(ToolArgs.Result(new { remembered = true, id = item.Id, status = item.Status }));
    }
}

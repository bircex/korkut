using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Matters;
using Korkut.Application.Matters.Requests;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>Opens a new matter (dosya) for the office, optionally linked to a client.</summary>
public sealed class CreateMatterTool(IMatterService matters) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "title": { "type": "string", "description": "Dosya başlığı" },
            "client_id": { "type": "string", "description": "Bağlanacak müvekkil kimliği (opsiyonel)" }
          },
          "required": ["title"]
        }
        """);

    public ToolSpec Spec => new("create_matter", "Büroya yeni bir dosya (dava) açar.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var title = ToolArgs.String(arguments, "title");
        if (string.IsNullOrWhiteSpace(title))
        {
            return ToolResult.Error("'title' gereklidir.");
        }

        var request = new CreateMatterRequest
        {
            Title = title.Trim(),
            ClientId = ToolArgs.TryGuid(arguments, "client_id", out var cid) ? cid : null,
        };
        var matter = await matters.CreateAsync(request, ct);
        return ToolResult.Ok(ToolArgs.Result(new { matter.Id, matter.Title, matter.Status }));
    }
}

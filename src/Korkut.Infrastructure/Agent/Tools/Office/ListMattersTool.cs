using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Matters;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Agent.Tools.Office;

/// <summary>Bürodaki dosyaları listeler.</summary>
public sealed class ListMattersTool(IMatterService matters) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        { "type": "object", "properties": {} }
        """);

    public ToolSpec Spec => new("list_matters", "Bürodaki dosyaları listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var result = await matters.ListAsync(ct);
        return ToolResult.Ok(ToolArgs.Result(result));
    }
}

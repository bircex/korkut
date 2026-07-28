using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Matters;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

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
        return result.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(result.Value!))
            : ToolResult.Error(result.Error!.Detail);
    }
}

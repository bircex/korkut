using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Matters;
using Korkut.Tools.Core;

namespace Korkut.Tools.Office;

/// <summary>Bir dosyanın ayrıntılarını (duruşmalar/taraflar dahil) getirir.</summary>
public sealed class GetMatterTool(IMatterService matters) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": { "matter_id": { "type": "string", "description": "Dosya kimliği" } },
          "required": ["matter_id"]
        }
        """);

    public ToolSpec Spec => new("get_matter", "Bir dosyanın ayrıntılarını (duruşmalar/taraflar dahil) getirir.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryGuid(arguments, "matter_id", out var id))
        {
            return ToolResult.Error("'matter_id' gereklidir.");
        }

        var matter = await matters.GetAsync(id, ct);
        return matter.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(matter.Value!))
            : ToolResult.Error(matter.Error!.Detail);
    }
}

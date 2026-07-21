using System.Text.Json;
using Korkut.Application.Features.OfficeCore;

namespace Korkut.Agent.Tools.Office;

/// <summary>Lists the office's matters (dosyalar/davalar).</summary>
public sealed class ListMattersTool(IOfficeScope scope, IMatterService matters) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse("""{"type":"object","properties":{}}""");

    public ToolSpec Spec => new("list_matters", "Bürodaki dosyaları (davaları) listeler.", Schema);

    public string? RequiresKey => null;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var list = await scope.RunAsync(context.OfficeId, c => matters.ListAsync(context.OfficeId, c), ct);
        return ToolResult.Ok(ToolArgs.Result(
            list.Select(m => new { m.Id, m.Title, m.Status, m.FileNo, m.CaseType, m.Counterparty })));
    }
}

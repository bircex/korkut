using System.Text.Json;
using Korkut.Application.Features.Membership;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Lists the office's members (avukatlar/çalışanlar) — e.g. to pick a task assignee.</summary>
public sealed class ListMembersTool(IOfficeScope scope, IMembersService members) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse("""{"type":"object","properties":{}}""");

    public ToolSpec Spec => new("list_members", "Bürodaki üyeleri (avukatları/çalışanları) listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var list = await scope.RunAsync(context.OfficeId, c => members.ListAsync(context.OfficeId, c), ct);
        return ToolResult.Ok(ToolArgs.Result(list));
    }
}

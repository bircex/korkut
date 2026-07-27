using Korkut.Core;
using System.Text.Json;
using Korkut.Contract.Organization;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;

namespace Korkut.Tools.Office;

/// <summary>Büro üyelerini listeler.</summary>
public sealed class ListMembersTool(IMemberService members) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        { "type": "object", "properties": {} }
        """);

    public ToolSpec Spec => new("list_members", "Büro üyelerini listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MembersManage;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var result = await members.ListAsync(ct);
        return result.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(result.Value!))
            : ToolResult.Error(result.Error!.Detail);
    }
}

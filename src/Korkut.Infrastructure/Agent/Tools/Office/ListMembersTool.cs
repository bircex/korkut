using System.Text.Json;
using Korkut.Application.Members;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Application.Security;

namespace Korkut.Infrastructure.Agent.Tools.Office;

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
        return ToolResult.Ok(ToolArgs.Result(result));
    }
}

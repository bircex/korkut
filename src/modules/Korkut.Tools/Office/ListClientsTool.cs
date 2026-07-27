using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Contract.Clients;

namespace Korkut.Tools.Office;

/// <summary>Bürodaki müvekkilleri listeler.</summary>
public sealed class ListClientsTool(IClientService clients) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        { "type": "object", "properties": {} }
        """);

    public ToolSpec Spec => new("list_clients", "Bürodaki müvekkilleri listeler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.ClientsRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var result = await clients.ListAsync(ct);
        return result.IsSuccess
            ? ToolResult.Ok(ToolArgs.Result(result.Value!))
            : ToolResult.Error(result.Error!.Detail);
    }
}

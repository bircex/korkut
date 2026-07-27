using Korkut.Core;
using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Infrastructure.Security;

namespace Korkut.Infrastructure.Agent.Tools;

/// <summary>
/// Signal tool: the request is a document drafting/editing task, so the document is prepared in the
/// right-hand EDITOR (not written into chat). Carries no side effect — it flags the turn; the host/
/// UI opens the editor. Only call it once the parties / subject / dates / amounts are known.
/// </summary>
public sealed class OpenEditorTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """{"type":"object","properties":{"title":{"type":"string","description":"Hazırlanacak belgenin başlığı (opsiyonel)"}}}""");

    public ToolSpec Spec => new(
        "open_editor",
        "Belge hazırlama/düzenleme isteğini editöre yönlendirir; belge sohbete değil sağdaki editörde hazırlanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var title = ToolArgs.String(arguments, "title");
        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new { open_editor = true, title })));
    }
}

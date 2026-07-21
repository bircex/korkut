using System.Text.Json;
using Korkut.Application.Features.OfficeCore;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Fetches one matter's detail together with its hearings.</summary>
public sealed class GetMatterTool(IOfficeScope scope, IMatterService matters) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """{"type":"object","properties":{"matter_id":{"type":"string","description":"Dosya kimliği"}},"required":["matter_id"]}""");

    public ToolSpec Spec => new("get_matter", "Bir dosyanın ayrıntısını ve duruşmalarını getirir.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersRead;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryGuid(arguments, "matter_id", out var id))
        {
            return ToolResult.Error("'matter_id' geçerli bir kimlik olmalıdır.");
        }

        var detail = await scope.RunAsync(context.OfficeId, c => matters.GetDetailAsync(context.OfficeId, id, c), ct);
        return detail is null
            ? ToolResult.Ok("Dosya bulunamadı.")
            : ToolResult.Ok(ToolArgs.Result(new { detail.Matter, detail.Hearings }));
    }
}

using System.Text.Json;
using Korkut.Application.Features.OfficeOps;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools.Office;

/// <summary>Marks a legal deadline as completed.</summary>
public sealed class CompleteDeadlineTool(IOfficeScope scope, IOfficeOpsService ops) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """{"type":"object","properties":{"deadline_id":{"type":"string"}},"required":["deadline_id"]}""");

    public ToolSpec Spec => new("complete_deadline", "Bir yasal süreyi tamamlandı olarak işaretler.", Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        if (!ToolArgs.TryGuid(arguments, "deadline_id", out var id))
        {
            return ToolResult.Error("'deadline_id' geçerli bir kimlik olmalıdır.");
        }

        var ok = await scope.RunAsync(context.OfficeId, c => ops.CompleteDeadlineAsync(context.OfficeId, id, c), ct);
        return ToolResult.Ok(ok ? "Süre tamamlandı olarak işaretlendi." : "Süre bulunamadı.");
    }
}

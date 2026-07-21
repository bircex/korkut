using System.Text.Json;
using Korkut.Application.Features.Outbox;
using Korkut.Application.Security;

namespace Korkut.Agent.Tools;

/// <summary>
/// Drafts an outbound e-mail into the office review queue. It does NOT send immediately: a human
/// reviews/confirms the draft before it goes out (the outbox pipeline), which is the safe path for
/// a legal product. Returns the draft id.
/// </summary>
public sealed class SendEmailTool(IOfficeScope scope, IOutboxService outbox) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "to": { "type": "string", "description": "Alıcı e-posta adresi" },
            "subject": { "type": "string", "description": "Konu" },
            "body": { "type": "string", "description": "E-posta metni" }
          },
          "required": ["to", "subject", "body"]
        }
        """);

    public ToolSpec Spec => new(
        "send_email",
        "Bir e-posta taslağı oluşturur ve onay kuyruğuna ekler (avukat onayladıktan sonra gönderilir).",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => Permissions.MattersWrite;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, ToolContext context, CancellationToken ct = default)
    {
        var to = ToolArgs.String(arguments, "to");
        var subject = ToolArgs.String(arguments, "subject");
        var body = ToolArgs.String(arguments, "body");
        
        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            return ToolResult.Error("'to', 'subject' ve 'body' gereklidir.");
        }

        var id = await scope.RunAsync(context.OfficeId,
            c => outbox.CreateDraftAsync(context.OfficeId, "email", to.Trim(), subject.Trim(), body, sendAt: null, c), ct);
        return ToolResult.Ok(ToolArgs.Result(new { drafted = true, id, note = "Onay kuyruğuna eklendi; gönderilmeden önce onaylanmalı." }));
    }
}

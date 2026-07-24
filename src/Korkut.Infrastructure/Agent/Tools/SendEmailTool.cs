using System.Text.Json;
using Korkut.Application.Agent.Models;
using Korkut.Application.Agent.Tools;
using Korkut.Core;
using Korkut.Application.Outbox;
using Korkut.Application.Security;
using Korkut.Domain.Outbox;

namespace Korkut.Infrastructure.Agent.Tools;

/// <summary>
/// Drafts an outbound e-mail into the office review queue. It does NOT send: a human reviews and
/// confirms the draft before it goes out (the outbox pipeline), the safe path for a legal product.
/// The office comes from the trusted caller, and the draft is written under that tenant scope.
/// </summary>
public sealed class SendEmailTool(
    IOutboxRepository outbox, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IAgentTool
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

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var to = ToolArgs.String(arguments, "to");
        var subject = ToolArgs.String(arguments, "subject");
        var body = ToolArgs.String(arguments, "body");
        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            return ToolResult.Error("'to', 'subject' ve 'body' gereklidir.");
        }

        var office = currentUser.OfficeId;
        var draft = new OutboundMessage(office, "email", to.Trim(), subject.Trim(), body, sendAt: null, clock.UtcNow);
        await unitOfWork.InTenantScopeAsync(office, async () =>
        {
            await outbox.AddAsync(draft, ct);
            return true;
        }, ct);

        return ToolResult.Ok(ToolArgs.Result(new { drafted = true, id = draft.Id, note = "Onay kuyruğuna eklendi; gönderilmeden önce onaylanmalı." }));
    }
}

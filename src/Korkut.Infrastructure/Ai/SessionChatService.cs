using System.Text;
using Korkut.Application.Ai;
using Korkut.Application.Runtime;
using Korkut.Infrastructure.Persistence;

namespace Korkut.Infrastructure.Ai;

/// <summary>The result of a chat turn: the assistant reply, the (possibly auto-set) title, and UI hints.</summary>
public sealed record ChatOutcome(string Reply, string Title, Guid? OpenDocument, Guid? PendingEmail);

/// <summary>
/// Orchestrates one assistant chat turn within the caller's tenant scope: persists the
/// user message, builds a system prompt from the office persona + the user's effective
/// drafting style + active office memory, sends the conversation to the LLM, persists the
/// reply, and auto-titles a fresh conversation from its first message. Document drafting
/// and outbox hints (open_document / pending_email) are wired in later brain slices.
/// </summary>
public sealed class SessionChatService(
    SessionStore sessions, OfficeSettingsStore settings, ILlmClient llm, RuntimeConfig config)
{
    public async Task<ChatOutcome?> ChatAsync(Guid officeId, Guid userId, Guid conversationId, string text, CancellationToken ct = default)
    {
        var conversation = await sessions.GetAsync(officeId, conversationId, ct);
        if (conversation is null)
        {
            return null;
        }

        await sessions.AppendMessageAsync(officeId, conversationId, "user", text, ct);

        var system = await BuildSystemPromptAsync(officeId, userId, ct);
        var history = await sessions.MessagesAsync(officeId, conversationId, ct);
        var messages = history.Select(m => new LlmMessage(m.Role, m.Content)).ToList();

        var reply = await llm.CompleteAsync(new LlmRequest(system, messages, config.ReplyHeadroom * 4), ct);

        await sessions.AppendMessageAsync(officeId, conversationId, "assistant", reply.Text, ct);

        // Auto-title a brand-new conversation from its first user message.
        var title = conversation.Title;
        if (conversation.Title == "Yeni görüşme")
        {
            title = Summarize(text);
            await sessions.SetTitleIfDefaultAsync(officeId, conversationId, title, ct);
        }

        return new ChatOutcome(reply.Text, title, OpenDocument: null, PendingEmail: null);
    }

    private async Task<string> BuildSystemPromptAsync(Guid officeId, Guid userId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sen Korkut'sun; Türk hukuku alanında çalışan bir avukatlık bürosuna yardımcı olan yapay zeka asistanısın.");
        sb.AppendLine("Yanıtların Türkçe, açık ve hukuken dikkatli olsun; kesin olmayan noktalarda uyarı ekle.");

        var persona = await settings.GetPersonaAsync(officeId, ct);
        if (persona is not null)
        {
            AppendIf(sb, "Üslup", persona.Tone);
            AppendIf(sb, "Uzmanlık", persona.Expertise);
            AppendIf(sb, "Ek kurallar", persona.ExtraRules);
        }

        var office = await settings.GetStyleAsync(officeId, "office", Guid.Empty, ct);
        var self = await settings.GetStyleAsync(officeId, "user", userId, ct);
        var style = self.Over(office);
        AppendIf(sb, "Antet", style.Letterhead);
        AppendIf(sb, "İmza", style.Signature);
        AppendIf(sb, "Yazım tonu", style.Tone);
        AppendIf(sb, "Standart metin", style.Boilerplate);

        var memory = (await settings.ListMemoryAsync(officeId, ct)).Where(m => m.Status == "active").ToList();
        if (memory.Count > 0)
        {
            sb.AppendLine("Bürosuna dair hatırlanan notlar:");
            foreach (var m in memory)
            {
                sb.Append("- ").AppendLine(m.Text);
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendIf(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sb.Append(label).Append(": ").AppendLine(value.Trim());
        }
    }

    private static string Summarize(string text)
    {
        var oneLine = text.ReplaceLineEndings(" ").Trim();
        return oneLine.Length <= 60 ? oneLine : oneLine[..60].TrimEnd() + "…";
    }
}

using Korkut.Contract.Documents;
using Korkut.Agent.Brain;
using Korkut.Agent.Model;
using System.Diagnostics;
using System.Text;
using Korkut.Settings.Entities;
using Korkut.Core;
using Korkut.Knowledge;
using Korkut.Settings.Repositories;
using Korkut.Tools.Core;

namespace Korkut.Agent.Chat;

/// <summary>
/// Orchestrates one assistant chat turn within the caller's tenant scope: persists the
/// user message, builds a system prompt from the office persona + the user's effective
/// drafting style + active office memory, sends the conversation to the LLM, persists the
/// reply, and auto-titles a fresh conversation from its first message. Document drafting
/// and outbox hints (open_document / pending_email) are wired in later brain slices.
/// </summary>
public sealed class SessionChatService(
    ISessionService sessions, 
    IPersonaRepository personas,
    IStyleProfileRepository styles,
    IMemoryRepository memories,
    RuntimeConfig config,
    ITextExtractorService extractorService,
    IKnowledgeSearchService knowledge,
    ILlmCallLogService callLogService,
    IUsageGovernorService governorService,
    IAgentToolLoopService toolLoopService) : ISessionChatService
{
    private const int MaxExtractedChars = 20_000;

    // How many grounded chunks to pull from the Knowledge service per turn, and how much of each
    // to show the model. Retrieval is best-effort: when Knowledge is unconfigured/unreachable the
    // search returns nothing and the turn proceeds ungrounded.
    private const int GroundingTopK = 6;

    private const int MaxGroundingCharsPerHit = 800;

    /// <summary>
    /// A chat turn that attaches an uploaded document: extracts its text, includes it in
    /// the user turn so the assistant can reason over it, and returns the reply plus a
    /// short display line for the UI. Returns null if the conversation is missing.
    /// </summary>
    public async Task<UploadOutcome?> UploadAsync(
        Guid officeId, Guid userId, Guid conversationId, string fileName, string contentType, Stream content, string text,
        IReadOnlySet<string> permissions, CancellationToken ct = default)
    {
        var conversation = await sessions.GetAsync(officeId, conversationId, ct);
        if (conversation is null)
        {
            return null;
        }

        var spans = await extractorService.ExtractAsync(content, contentType, ct);
        var extracted = string.Join("\n\n", spans.Select(s => s.Text));
        if (extracted.Length > MaxExtractedChars)
        {
            extracted = extracted[..MaxExtractedChars] + "\n…(kısaltıldı)";
        }

        var userDisplay = string.IsNullOrWhiteSpace(text) ? $"📎 {fileName}" : $"{text}\n📎 {fileName}";
        var promptContent = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(text))
        {
            promptContent.AppendLine(text).AppendLine();
        }

        promptContent.AppendLine($"[Ekli belge: {fileName}]");
        promptContent.Append(extracted.Length > 0 ? extracted : "(belgeden metin çıkarılamadı)");

        await sessions.AppendMessageAsync(officeId, conversationId, "user", promptContent.ToString(), ct);

        var system = await BuildSystemPromptAsync(officeId, userId, ct);
        system = await WithGroundingAsync(system, string.IsNullOrWhiteSpace(text) ? fileName : text, ct);
        var history = await sessions.MessagesAsync(officeId, conversationId, ct);
        var messages = history.Select(m => new ChatMessage(m.Role, m.Content)).ToList();
        await using var governed = await governorService.BeginCallAsync(officeId, ct);
        var timer = Stopwatch.StartNew();
        var reply = await toolLoopService.RunAsync(
            system, messages, new AgentToolContext(officeId, userId, permissions),
            ModelSpecialty.Dispatcher, config.ReplyHeadroom * 4, ct: ct);
        timer.Stop();
        await RecordCallAsync(officeId, LlmFeature.Chat, reply.Usage, timer.Elapsed, PromptFingerprint.Of(system), ct);
        await sessions.AppendMessageAsync(officeId, conversationId, "assistant", reply.Text, ct);

        var title = conversation.Title;
        if (conversation.Title == "Yeni görüşme")
        {
            title = Summarize(string.IsNullOrWhiteSpace(text) ? fileName : text);
            await sessions.SetTitleIfDefaultAsync(officeId, conversationId, title, ct);
        }

        return new UploadOutcome(reply.Text, title, userDisplay);
    }

    public async Task<ChatOutcome?> ChatAsync(
        Guid officeId, Guid userId, Guid conversationId, string text, IReadOnlySet<string> permissions, CancellationToken ct = default)
    {
        var conversation = await sessions.GetAsync(officeId, conversationId, ct);
        if (conversation is null)
        {
            return null;
        }

        await sessions.AppendMessageAsync(officeId, conversationId, "user", text, ct);

        var system = await BuildSystemPromptAsync(officeId, userId, ct);
        system = await WithGroundingAsync(system, text, ct);
        var history = await sessions.MessagesAsync(officeId, conversationId, ct);
        var messages = history.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

        await using var governed = await governorService.BeginCallAsync(officeId, ct);
        var timer = Stopwatch.StartNew();
        var reply = await toolLoopService.RunAsync(
            system, messages, new AgentToolContext(officeId, userId, permissions),
            ModelSpecialty.Dispatcher, config.ReplyHeadroom * 4, ct: ct);
        timer.Stop();
        await RecordCallAsync(officeId, LlmFeature.Chat, reply.Usage, timer.Elapsed, PromptFingerprint.Of(system), ct);

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

    /// <summary>
    /// Streaming twin of <see cref="ChatAsync"/>: fires <paramref name="onStep"/> for
    /// activity labels and <paramref name="onToken"/> for reply deltas as they arrive,
    /// persists the accumulated reply, and returns the same outcome. Returns null if the
    /// conversation is missing.
    /// </summary>
    public async Task<ChatOutcome?> ChatStreamAsync(
        Guid officeId, Guid userId, Guid conversationId, string text, IReadOnlySet<string> permissions,
        Func<string, Task> onStep, Func<string, Task> onToken, CancellationToken ct = default)
    {
        var conversation = await sessions.GetAsync(officeId, conversationId, ct);
        if (conversation is null)
        {
            return null;
        }

        await onStep("Bağlam hazırlanıyor");
        await sessions.AppendMessageAsync(officeId, conversationId, "user", text, ct);
        var system = await BuildSystemPromptAsync(officeId, userId, ct);
        system = await WithGroundingAsync(system, text, ct);
        var history = await sessions.MessagesAsync(officeId, conversationId, ct);
        var messages = history.Select(m => new ChatMessage(m.Role, m.Content)).ToList();

        await onStep("Yanıt üretiliyor");
        await using var governed = await governorService.BeginCallAsync(officeId, ct);
        var timer = Stopwatch.StartNew();
        // The loop reports tool activity through onStep and the final answer through onToken. When the
        // model uses no tools, the answer arrives as a single onToken chunk (the loop resolves first).
        var reply = await toolLoopService.RunAsync(
            system, messages, new AgentToolContext(officeId, userId, permissions),
            ModelSpecialty.Dispatcher, config.ReplyHeadroom * 4, onStep, onToken, ct);
        timer.Stop();
        await RecordCallAsync(officeId, LlmFeature.Chat, reply.Usage, timer.Elapsed, PromptFingerprint.Of(system), ct);

        var replyText = reply.Text;
        await sessions.AppendMessageAsync(officeId, conversationId, "assistant", replyText, ct);

        var title = conversation.Title;
        if (conversation.Title == "Yeni görüşme")
        {
            title = Summarize(text);
            await sessions.SetTitleIfDefaultAsync(officeId, conversationId, title, ct);
        }

        return new ChatOutcome(replyText, title, OpenDocument: null, PendingEmail: null);
    }

    /// <summary>
    /// Retrieve grounded chunks for <paramref name="query"/> from the Knowledge service and append
    /// them to the system prompt as a clearly-labelled citation block. Best-effort: when Knowledge
    /// is unconfigured/unreachable the search yields nothing and the original prompt is returned
    /// unchanged, so a chat turn never fails for lack of retrieval.
    /// </summary>
    private async Task<string> WithGroundingAsync(string system, string query, CancellationToken ct)
    {
        var hits = await knowledge.SearchAsync(query, GroundingTopK, ct);
        if (hits.Count == 0)
        {
            return system;
        }

        var sb = new StringBuilder(system);
        sb.AppendLine().AppendLine();
        sb.AppendLine("İlgili büro belgelerinden alıntılar (yalnızca ilgiliyse dayanak al, alakasızsa yok say):");
        foreach (var hit in hits)
        {
            var text = hit.Text.Length > MaxGroundingCharsPerHit ? hit.Text[..MaxGroundingCharsPerHit] + "…" : hit.Text;
            sb.Append("- ").AppendLine(text.ReplaceLineEndings(" ").Trim());
        }

        return sb.ToString().TrimEnd();
    }

    // Record the model call for cost/telemetry. Best-effort: a telemetry failure must never break
    // the chat turn, so any error here is swallowed (cancellation still propagates).
    private async Task RecordCallAsync(Guid officeId, string feature, ChatUsage usage, TimeSpan latency, string promptFingerprint, CancellationToken ct)
    {
        try
        {
            await callLogService.RecordAsync(officeId, feature, usage, latency, promptFingerprint, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Telemetry is best-effort; never fail a turn because the ledger write failed.
        }
    }

    private async Task<string> BuildSystemPromptAsync(Guid officeId, Guid userId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sen Korkut'sun; Türk hukuku alanında çalışan bir avukatlık bürosuna yardımcı olan yapay zeka asistanısın.");
        sb.AppendLine("Yanıtların Türkçe, açık ve hukuken dikkatli olsun; kesin olmayan noktalarda uyarı ekle.");

        var persona = await personas.FindAsync(officeId, ct);
        if (persona is not null)
        {
            AppendIf(sb, "Üslup", persona.Tone);
            AppendIf(sb, "Uzmanlık", persona.Expertise);
            AppendIf(sb, "Ek kurallar", persona.ExtraRules);
        }

        var office = await StyleAsync(officeId, StyleScope.Office, Guid.Empty, ct);
        var self = await StyleAsync(officeId, StyleScope.User, userId, ct);
        var style = self.Over(office);
        AppendIf(sb, "Antet", style.Letterhead);
        AppendIf(sb, "İmza", style.Signature);
        AppendIf(sb, "Yazım tonu", style.Tone);
        AppendIf(sb, "Standart metin", style.Boilerplate);

        var memory = (await memories.ListAsync(officeId, ct)).Where(m => m.Status == MemoryItem.Active).ToList();
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

    /// <summary>A style layer that was never set reads as empty, so the overlay always has a baseline.</summary>
    private async Task<StyleFields> StyleAsync(Guid officeId, string scope, Guid userId, CancellationToken ct)
        => (await styles.FindAsync(officeId, scope, userId, ct))?.Fields ?? StyleFields.Empty;

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

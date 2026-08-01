using Korkut.Agent.Drafting.Entities;
using Korkut.Agent.Drafting.Repositories;
using Korkut.Agent.Drafting.Abstractions;
using Korkut.Contract.Agent;
using Korkut.Contract.Agent.Drafting;
using Korkut.Contract.Agent.Model;
using System.Diagnostics;
using System.Text;
using Korkut.Settings;
using Korkut.Settings.Entities;
using Korkut.Contract.Templates;
using Korkut.Settings.Repositories;

namespace Korkut.Agent.Drafting.Services;

/// <summary>
/// Produces AI edits to a generated document. Builds a drafting-focused system prompt
/// (office persona + effective style), seeds a fresh document from the requested petition
/// template's built-in default when there's no content yet, asks the LLM to return the
/// full updated markdown, and stages it as a pending edit for human accept/revert.
/// </summary>
internal sealed class DraftingService(
    IDraftDocumentService documents, IPersonaRepository personas, IStyleProfileRepository styles, TemplateCatalog catalog, IChatModelService llm,
    ILlmCallLogService callLogService, IUsageGovernorService governorService, IAgentUnitOfWork uow) : IDraftingService
{
    public async Task<DraftResult?> DraftAsync(
        Guid officeId, Guid userId, Guid docId, string instruction, string? petitionType, CancellationToken ct = default)
    {
        var doc = await documents.GetAsync(officeId, docId, ct);
        if (doc is null)
        {
            return null;
        }

        var baseContent = doc.ContentMd;
        if (string.IsNullOrWhiteSpace(baseContent) && !string.IsNullOrWhiteSpace(petitionType))
        {
            baseContent = catalog.DefaultContent(TemplateCatalog.Dilekce, petitionType!)
                ?? catalog.DefaultContent(TemplateCatalog.Sozlesme, petitionType!)
                ?? string.Empty;
        }

        var system = await ComposeSystemAsync(officeId, userId, petitionType, ct);

        await using var governed = await governorService.BeginCallAsync(officeId, ct);
        var timer = Stopwatch.StartNew();
        var reply = await llm.CompleteAsync(
            new ChatRequest(system, [new ChatMessage("user", BuildUserPrompt(baseContent, instruction))], ModelSpecialty.Petition, MaxTokens: 8000), ct);
        timer.Stop();
        try
        {
            await callLogService.RecordAsync(officeId, LlmFeature.Drafting, reply.Usage, timer.Elapsed, PromptFingerprint.Of(system), ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Telemetry is best-effort; never fail drafting because the ledger write failed.
        }

        var summary = Summarize(instruction);
        var pendingId = await documents.StagePendingAsync(officeId, docId, reply.Text, summary, ct);
        return pendingId is null ? null : new DraftResult(pendingId.Value, reply.Text, summary);
    }

    public async Task<SessionDraftResult?> DraftForSessionAsync(
        Guid officeId, Guid userId, Guid conversationId, string? title, string instruction, string? petitionType,
        CancellationToken ct = default)
    {
        // Phase 1 — short DUAL scope (persona/style live on the operational DB, the document on the
        // Agent DB): resolve or create the conversation's document and snapshot the prompts, so no
        // connection is held while the model drafts.
        var prep = await uow.InDualScopeAsync(officeId, async () =>
        {
            var wanted = string.IsNullOrWhiteSpace(title) ? Summarize(instruction) : title!.Trim();
            var doc = await documents.GetOrCreateForSessionAsync(officeId, conversationId, wanted, ct);
            var baseContent = doc.ContentMd;
            if (string.IsNullOrWhiteSpace(baseContent) && !string.IsNullOrWhiteSpace(petitionType))
            {
                baseContent = catalog.DefaultContent(TemplateCatalog.Dilekce, petitionType!)
                    ?? catalog.DefaultContent(TemplateCatalog.Sozlesme, petitionType!)
                    ?? string.Empty;
            }

            var system = await ComposeSystemAsync(officeId, userId, petitionType, ct);
            return new SessionPrep(doc.Id, doc.Title, system, BuildUserPrompt(baseContent, instruction));
        }, ct);

        // Phase 2 — NO scope: the model drafts. The surrounding chat turn already holds the
        // office's governed slot (this runs as a chat tool), so no second BeginCallAsync here —
        // a nested slot could starve the office's own concurrency budget.
        var timer = Stopwatch.StartNew();
        var reply = await llm.CompleteAsync(
            new ChatRequest(prep.System, [new ChatMessage("user", prep.Prompt)], ModelSpecialty.Petition, MaxTokens: 8000), ct);
        timer.Stop();

        // Phase 3 — short agent scope: stage the pending edit for human accept/revert.
        var summary = Summarize(instruction);
        var pendingId = await uow.InScopeAsync(
            officeId, () => documents.StagePendingAsync(officeId, prep.DocId, reply.Text, summary, ct), ct);
        await RecordDraftCallAsync(officeId, reply.Usage, timer.Elapsed, prep.System, ct);
        return pendingId is null ? null : new SessionDraftResult(prep.DocId, prep.Title, summary);
    }

    /// <summary>Phase-1 snapshot handed to the (scope-free) model call.</summary>
    private sealed record SessionPrep(Guid DocId, string Title, string System, string Prompt);

    // The drafting system prompt: persona/style plus, when a petition type is targeted, the type's
    // format-aware guide (ordered sections + HMK-critical flags) and the case-specific intake block
    // (inherited jurisdiction/deadlines/evidence + the type's mandatory questions/checks) so drafting
    // follows the Turkish legal format and never silently drops a critical section.
    private async Task<string> ComposeSystemAsync(Guid officeId, Guid userId, string? petitionType, CancellationToken ct)
    {
        var system = await BuildDraftingPromptAsync(officeId, userId, ct);
        var guide = string.IsNullOrWhiteSpace(petitionType) ? null : catalog.DraftingInstruction(petitionType!);
        return string.IsNullOrWhiteSpace(guide)
            ? system
            : system + "\n\nBu belge tipi için format ve olay yönergesi (uy):\n" + guide;
    }

    private static string BuildUserPrompt(string baseContent, string instruction)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Aşağıdaki belgeyi verilen talimata göre güncelle ve YALNIZCA güncellenmiş belgeyi markdown olarak döndür.");
        prompt.AppendLine().AppendLine("--- Mevcut belge ---");
        prompt.AppendLine(string.IsNullOrWhiteSpace(baseContent) ? "(boş)" : baseContent);
        prompt.AppendLine().AppendLine("--- Talimat ---").AppendLine(instruction);
        return prompt.ToString();
    }

    // Ledger write in its own short scope so a telemetry failure can never abort the staged edit.
    private async Task RecordDraftCallAsync(Guid officeId, ChatUsage usage, TimeSpan latency, string system, CancellationToken ct)
    {
        try
        {
            await uow.InScopeAsync(officeId, async () =>
            {
                await callLogService.RecordAsync(officeId, LlmFeature.Drafting, usage, latency, PromptFingerprint.Of(system), ct);
                return true;
            }, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Telemetry is best-effort; never fail drafting because the ledger write failed.
        }
    }

    private async Task<string> BuildDraftingPromptAsync(Guid officeId, Guid userId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sen Korkut'sun; Türk hukuku alanında dilekçe ve sözleşme taslakları hazırlayan bir asistansın.");
        sb.AppendLine("Çıktı temiz, biçimli markdown olsun; standart bölümleri koru ve hukuken dikkatli yaz.");

        var persona = await personas.FindAsync(officeId, ct);
        if (persona is not null && !string.IsNullOrWhiteSpace(persona.Tone))
        {
            sb.Append("Üslup: ").AppendLine(persona.Tone.Trim());
        }

        var style = (await StyleAsync(officeId, StyleScope.User, userId, ct))
            .Over(await StyleAsync(officeId, StyleScope.Office, Guid.Empty, ct));
        if (!string.IsNullOrWhiteSpace(style.Letterhead))
        {
            sb.Append("Antet: ").AppendLine(style.Letterhead.Trim());
        }

        if (!string.IsNullOrWhiteSpace(style.Signature))
        {
            sb.Append("İmza: ").AppendLine(style.Signature.Trim());
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>A style layer that was never set reads as empty, so the overlay always has a baseline.</summary>
    private async Task<StyleFields> StyleAsync(Guid officeId, string scope, Guid userId, CancellationToken ct)
        => (await styles.FindAsync(officeId, scope, userId, ct))?.Fields ?? StyleFields.Empty;

    private static string Summarize(string instruction)
    {
        var oneLine = instruction.ReplaceLineEndings(" ").Trim();
        return oneLine.Length <= 80 ? oneLine : oneLine[..80].TrimEnd() + "…";
    }
}

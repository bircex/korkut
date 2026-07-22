using System.Diagnostics;
using System.Text;
using Korkut.Application.Ai;
using Korkut.Agent.Application.Ai;
using Korkut.Agent.Application.Abstractions.Ai;
using Korkut.Agent.Application.Abstractions.Persistence;
using Korkut.Agent.Application.Abstractions.Sessions;
using Korkut.Agent.Application.Sessions;
using Korkut.Agent.Application.Chat;
using Korkut.Application.Features.Settings;
using Korkut.Infrastructure.Templates;

namespace Korkut.Agent.Application.Chat;

/// <summary>A staged pending edit: its id, the proposed markdown, and a short summary.</summary>
public sealed record DraftResult(Guid PendingId, string ContentMd, string Summary);

/// <summary>
/// Produces AI edits to a generated document. Builds a drafting-focused system prompt
/// (office persona + effective style), seeds a fresh document from the requested petition
/// template's built-in default when there's no content yet, asks the LLM to return the
/// full updated markdown, and stages it as a pending edit for human accept/revert.
/// </summary>
public sealed class DraftingService(
    IDraftDocumentService documents, IOfficeSettingsService settings, TemplateCatalog catalog, IChatModel llm,
    ILlmCallLog callLog, IUsageGovernor governor)
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

        var system = await BuildDraftingPromptAsync(officeId, userId, ct);
        var prompt = new StringBuilder();
        prompt.AppendLine("Aşağıdaki belgeyi verilen talimata göre güncelle ve YALNIZCA güncellenmiş belgeyi markdown olarak döndür.");
        prompt.AppendLine().AppendLine("--- Mevcut belge ---");
        prompt.AppendLine(string.IsNullOrWhiteSpace(baseContent) ? "(boş)" : baseContent);
        prompt.AppendLine().AppendLine("--- Talimat ---").AppendLine(instruction);

        await using var governed = await governor.BeginCallAsync(officeId, ct);
        var timer = Stopwatch.StartNew();
        var reply = await llm.CompleteAsync(
            new ChatRequest(system, [new ChatMessage("user", prompt.ToString())], ModelSpecialty.Petition, MaxTokens: 8000), ct);
        timer.Stop();
        try
        {
            await callLog.RecordAsync(officeId, LlmFeature.Drafting, reply.Usage, timer.Elapsed, PromptFingerprint.Of(system), ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // Telemetry is best-effort; never fail drafting because the ledger write failed.
        }

        var summary = Summarize(instruction);
        var pendingId = await documents.StagePendingAsync(officeId, docId, reply.Text, summary, ct);
        return pendingId is null ? null : new DraftResult(pendingId.Value, reply.Text, summary);
    }

    private async Task<string> BuildDraftingPromptAsync(Guid officeId, Guid userId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sen Korkut'sun; Türk hukuku alanında dilekçe ve sözleşme taslakları hazırlayan bir asistansın.");
        sb.AppendLine("Çıktı temiz, biçimli markdown olsun; standart bölümleri koru ve hukuken dikkatli yaz.");

        var persona = await settings.GetPersonaAsync(officeId, ct);
        if (persona is not null && !string.IsNullOrWhiteSpace(persona.Tone))
        {
            sb.Append("Üslup: ").AppendLine(persona.Tone.Trim());
        }

        var style = (await settings.GetStyleAsync(officeId, "user", userId, ct)).Over(await settings.GetStyleAsync(officeId, "office", Guid.Empty, ct));
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

    private static string Summarize(string instruction)
    {
        var oneLine = instruction.ReplaceLineEndings(" ").Trim();
        return oneLine.Length <= 80 ? oneLine : oneLine[..80].TrimEnd() + "…";
    }
}

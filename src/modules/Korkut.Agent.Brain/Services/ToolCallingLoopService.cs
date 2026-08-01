using Korkut.Agent.Brain.Abstractions;
using Korkut.Tools.Core;
using Korkut.Contract.Agent.Model;
using System.Text;

namespace Korkut.Agent.Brain.Services;

/// <summary>
/// The assistant's tool-calling loop. It offers the CALLER'S permitted tools to the model (the
/// catalog filters by key + RBAC), runs any tool the model calls — office-scoped (RLS) and
/// permission-checked via the catalog — feeds the results back, and repeats until the model returns
/// a final text answer or the iteration cap is reached. The model, not the loop, decides which tools
/// to call; adding a tool never changes this loop. Implements <see cref="IAgentToolLoopService"/> so
/// lower layers (the chat service) can drive a tool-augmented turn without depending on the tools.
/// </summary>
internal sealed class ToolCallingLoopService(IChatModelService llm, IToolCatalog catalog) : IAgentToolLoopService
{
    private const int MaxIterations = 8;

    public async Task<ChatReply> RunAsync(
        string system, IReadOnlyList<ChatMessage> messages, AgentToolContext context,
        string specialty, int? maxTokens = null,
        Func<string, Task>? onStep = null, Func<string, Task>? onToken = null,
        Func<string, Task>? onTool = null, CancellationToken ct = default)
    {
        var tools = catalog.AvailableSpecs(context)
            .Select(s => new ToolDefinition(s.Name, s.Description, s.Parameters))
            .ToList();

        var turn = messages.ToList();
        var prompt = 0;
        var completion = 0;
        var provider = string.Empty;
        var modelId = string.Empty;
        // Legal sources surfaced by tool results, in call order; dedup/caps are the chat service's job.
        List<Korkut.Contract.Agent.Chat.SourceRef>? sources = null;
        // The draft document a tool call produced (draft_document); the last one wins.
        Korkut.Contract.Agent.Chat.DocumentRef? document = null;

        for (var i = 0; i < MaxIterations; i++)
        {
            // Stream the turn: text deltas reach the user token-by-token via onToken as they arrive,
            // and any tool calls are collected to run before the next turn.
            var (text, toolCalls, usage) = await StreamTurnAsync(system, turn, specialty, maxTokens, tools, onToken, ct);
            (prompt, completion, provider, modelId) = Accumulate(prompt, completion, usage);

            if (toolCalls is not { Count: > 0 } calls)
            {
                return new ChatReply(
                    text, new ChatUsage(provider, modelId, prompt, completion),
                    Sources: CitedSources(sources, text), Document: document);
            }

            // Record the assistant's tool-call turn, then run each call and feed the results back.
            turn.Add(new ChatMessage("assistant", text, ToolCalls: calls));
            var outputs = new List<ToolOutput>(calls.Count);
            foreach (var call in calls)
            {
                if (onStep is not null)
                {
                    await onStep(StepLabel(call));
                }

                if (onTool is not null)
                {
                    await onTool(call.Name);
                }

                var result = await catalog.InvokeAsync(call.Name, call.Arguments, context, ct);
                outputs.Add(new ToolOutput(call.Id, result.Content, result.IsError));
                if (result.Sources is { Count: > 0 })
                {
                    (sources ??= []).AddRange(result.Sources);
                }

                if (result.Document is { } doc)
                {
                    document = doc;
                }
            }

            turn.Add(new ChatMessage("tool", string.Empty, ToolResults: outputs));
        }

        // Iteration cap reached — ask once more (streamed) WITHOUT tools to force a final answer.
        var (finalText, _, finalUsage) = await StreamTurnAsync(system, turn, specialty, maxTokens, tools: null, onToken, ct);
        (prompt, completion, provider, modelId) = Accumulate(prompt, completion, finalUsage);
        return new ChatReply(
            finalText, new ChatUsage(provider, modelId, prompt, completion),
            Sources: CitedSources(sources, finalText), Document: document);
    }

    // Drive one streamed model turn: append text deltas (live via onToken), collect any tool calls,
    // and capture usage. The provider emits a single tool-calls event once the turn is a tool-use turn.
    private async Task<(string Text, IReadOnlyList<ToolInvocation>? ToolCalls, ChatUsage Usage)> StreamTurnAsync(
        string system, IReadOnlyList<ChatMessage> turn, string specialty, int? maxTokens,
        IReadOnlyList<ToolDefinition>? tools, Func<string, Task>? onToken, CancellationToken ct)
    {
        var text = new StringBuilder();
        IReadOnlyList<ToolInvocation>? toolCalls = null;
        var usage = ChatUsage.None;

        await foreach (var ev in llm.StreamAsync(new ChatRequest(system, turn, specialty, maxTokens, Tools: tools), ct))
        {
            if (ev.Delta is { Length: > 0 } delta)
            {
                text.Append(delta);
                if (onToken is not null)
                {
                    await onToken(delta);
                }
            }

            if (ev.ToolCalls is { Count: > 0 } calls)
            {
                toolCalls = calls;
            }

            if (ev.Usage is { } u)
            {
                usage = u;
            }
        }

        return (text.ToString(), toolCalls, usage);
    }

    // Sum token counts across the loop's model calls; keep the last call's provider/model for the label.
    private static (int Prompt, int Completion, string Provider, string ModelId) Accumulate(
        int prompt, int completion, ChatUsage usage)
        => (prompt + usage.PromptTokens, completion + usage.CompletionTokens, usage.Provider, usage.ModelId);

    // ---- user-facing activity labels ------------------------------------------------------------

    private static readonly Dictionary<string, string> SourceNames = new()
    {
        ["yargitay"] = "Yargıtay", ["danistay"] = "Danıştay", ["emsal"] = "UYAP Emsal",
        ["kyb"] = "Kanun Yararına Bozma", ["anayasa"] = "Anayasa Mahkemesi", ["mevzuat"] = "mevzuat",
        ["gib"] = "GİB", ["kik"] = "KİK", ["rekabet"] = "Rekabet Kurumu", ["sayistay"] = "Sayıştay",
        ["uyusmazlik"] = "Uyuşmazlık Mahkemesi", ["bddk"] = "BDDK", ["kvkk"] = "KVKK",
        ["sigorta_tahkim"] = "Sigorta Tahkim",
    };

    private static readonly Dictionary<string, string> StepLabels = new()
    {
        ["search_office_documents"] = "Büro belgelerinde arıyorum",
        ["search_within_mevzuat"] = "Mevzuat içinde arıyorum",
        ["search_within_sigorta_tahkim"] = "Sigorta Tahkim kararlarında arıyorum",
        ["get_mevzuat_madde_tree"] = "Kanunun madde ağacına bakıyorum",
        ["get_mevzuat_gerekce"] = "Madde gerekçesine bakıyorum",
        ["draft_document"] = "Belge taslağını hazırlıyorum",
        ["list_clients"] = "Müvekkil listesine bakıyorum",
        ["create_client"] = "Müvekkil kaydı oluşturuyorum",
        ["list_matters"] = "Dosyalara bakıyorum",
        ["get_matter"] = "Dosyayı inceliyorum",
        ["create_matter"] = "Dosya açıyorum",
        ["add_hearing"] = "Duruşma ekliyorum",
        ["add_deadline"] = "Süre ekliyorum",
        ["list_deadlines"] = "Süreleri kontrol ediyorum",
        ["upcoming_agenda"] = "Ajandaya bakıyorum",
        ["list_events"] = "Etkinliklere bakıyorum",
        ["create_event"] = "Etkinlik oluşturuyorum",
        ["add_task"] = "Görev ekliyorum",
        ["list_tasks"] = "Görevlere bakıyorum",
        ["complete_task"] = "Görevi tamamlıyorum",
        ["add_note"] = "Not ekliyorum",
        ["list_notes"] = "Notlara bakıyorum",
        ["add_invoice"] = "Fatura kaydediyorum",
        ["list_invoices"] = "Faturalara bakıyorum",
        ["add_expense"] = "Masraf kaydediyorum",
        ["list_expenses"] = "Masraflara bakıyorum",
        ["finance_summary"] = "Mali özeti çıkarıyorum",
        ["add_evrak"] = "Evrak kaydediyorum",
        ["list_evrak"] = "Evraka bakıyorum",
        ["list_members"] = "Büro üyelerine bakıyorum",
        ["office_overview"] = "Büro durumuna bakıyorum",
        ["remember"] = "Not alıyorum",
    };

    /// <summary>
    /// The human label streamed while a tool runs ("Yargıtay kararlarında arıyorum" — never the raw
    /// tool name). Search/fetch derive the database name from the call's <c>source</c> argument.
    /// </summary>
    private static string StepLabel(ToolInvocation call)
    {
        if (call.Name is "search_decisions" or "fetch_document")
        {
            var source = ToolArgs.String(call.Arguments, "source");
            var name = source is not null && SourceNames.TryGetValue(source, out var s) ? s : null;
            return call.Name == "search_decisions"
                ? (name is null ? "İçtihat ve mevzuatta arıyorum" : name == "mevzuat" ? "Mevzuatta arıyorum" : $"{name} kararlarında arıyorum")
                : (name is null ? "Belgenin tam metnini okuyorum" : name == "mevzuat" ? "Kanun metnini okuyorum" : $"{name} kararının tam metnini okuyorum");
        }

        return StepLabels.TryGetValue(call.Name, out var label) ? label : "Üzerinde çalışıyorum";
    }

    // ---- source relevance -----------------------------------------------------------------------

    /// <summary>
    /// Keep only the sources the final answer actually cites (esas/karar numarası, or for
    /// legislation the law number/title). The search tools pull candidate hits; many turn out
    /// irrelevant and the model discards them while reasoning — showing those in the drawer erodes
    /// trust. Citation discipline makes the model number everything it relies on, so a source that
    /// is never mentioned in the answer is noise. No citations → no sources chip.
    /// </summary>
    private static List<Korkut.Contract.Agent.Chat.SourceRef>? CitedSources(
        List<Korkut.Contract.Agent.Chat.SourceRef>? sources, string finalText)
    {
        if (sources is not { Count: > 0 })
        {
            return sources;
        }

        var cited = sources.Where(s => Cites(finalText, s)).ToList();
        return cited.Count > 0 ? cited : null;
    }

    private static bool Cites(string text, Korkut.Contract.Agent.Chat.SourceRef s)
    {
        if (Mentions(text, s.DecisionNumber) || Mentions(text, s.CaseNumber))
        {
            return true;
        }

        // Legislation: the id is the law number ("5816") and answers cite "5816 sayılı"; long
        // titles ("Türk Ceza Kanunu") also count. Case-law ids are opaque tokens — never matched.
        return s.Kind == "mevzuat"
            && (Mentions(text, s.Id.Length >= 3 ? s.Id : null) || Mentions(text, s.Title.Length >= 10 ? s.Title : null));
    }

    private static bool Mentions(string text, string? needle)
        => !string.IsNullOrWhiteSpace(needle) && text.Contains(needle, StringComparison.OrdinalIgnoreCase);
}

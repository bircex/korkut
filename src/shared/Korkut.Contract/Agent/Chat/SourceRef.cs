namespace Korkut.Contract.Agent.Chat;

/// <summary>
/// One legal source the assistant consulted during a chat turn, normalized for the UI's
/// "İlgili Kararlar" drawer (see korkut-ui/docs/DRAWER_ROADMAP.md). Produced by the legal search
/// tools, accumulated by the tool loop, deduplicated/capped by the chat service, then delivered
/// three ways off the same value: the SSE <c>sources</c> frame, the chat response, and the
/// persisted assistant message (chat_messages.sources_json).
/// </summary>
/// <param name="Kind">"ictihat" (case law) or "mevzuat" (legislation) — the drawer groups by this.</param>
/// <param name="Source">The database token ("yargitay", "danistay", "mevzuat", …).</param>
/// <param name="Id">The id <c>fetch_document</c> accepts for this source (full-text lookup).</param>
public sealed record SourceRef(
    string Kind,
    string Source,
    string Id,
    string Title,
    string? Date = null,
    string? CaseNumber = null,
    string? DecisionNumber = null,
    string? Court = null,
    string? Chamber = null,
    string? Url = null,
    string? Snippet = null);

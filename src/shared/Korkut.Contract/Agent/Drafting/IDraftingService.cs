namespace Korkut.Contract.Agent.Drafting;

/// <summary>Composes an AI-drafted document edit (stages a pending version) within a tenant-scoped unit of work.</summary>
public interface IDraftingService
{
    Task<DraftResult?> DraftAsync(
        Guid officeId, Guid userId, Guid docId, string instruction, string? petitionType, CancellationToken ct = default);

    /// <summary>
    /// The chat-tool entry point: gets or creates the conversation's draft document, drafts into it
    /// per <paramref name="instruction"/>, and stages the result as a pending edit for human
    /// accept/revert. Owns its own (short) tenant scopes — safe to call from the transaction-free
    /// tool loop. Returns null when the office/document cannot be resolved.
    /// </summary>
    Task<SessionDraftResult?> DraftForSessionAsync(
        Guid officeId, Guid userId, Guid conversationId, string? title, string instruction, string? petitionType,
        CancellationToken ct = default);
}

/// <summary>The staged chat-drafted document: its id (for the UI's document card/editor), title, and change summary.</summary>
public sealed record SessionDraftResult(Guid DocId, string Title, string Summary);

namespace Korkut.Contract.Agent.Drafting;

/// <summary>Composes an AI-drafted document edit (stages a pending version) within a tenant-scoped unit of work.</summary>
public interface IDraftingService
{
    Task<DraftResult?> DraftAsync(
        Guid officeId, Guid userId, Guid docId, string instruction, string? petitionType, CancellationToken ct = default);
}

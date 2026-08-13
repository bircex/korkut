using Korkut.Agent.Persistence;
using Korkut.Agent.Ledger;
using Korkut.Agent.Ledger.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Agent.Persistence;

/// <summary>EF adapter over the Agent DB <c>llm_calls</c> ledger (RLS-scoped by the surrounding unit of work).</summary>
public sealed class LlmCallRepository(AgentDbContext db) : EfRepository<LlmCall>(db), ILlmCallRepository
{
    public async Task AddAsync(LlmCallEntry entry, CancellationToken ct = default)
    {
        Db.LlmCalls.Add(new LlmCall(
            entry.OfficeId, entry.UserId, entry.Feature, entry.Provider, entry.ModelId, entry.PromptFingerprint,
            entry.PromptTokens, entry.CompletionTokens, entry.Cost, entry.LatencyMs, entry.CreatedAt));
        await Db.SaveChangesAsync(ct);
    }

    public Task<long> SumMonthTokensAsync(Guid officeId, DateTimeOffset monthStart, CancellationToken ct = default)
        => Db.LlmCalls
            .Where(c => c.OfficeId == officeId && c.CreatedAt >= monthStart)
            .SumAsync(c => (long)c.PromptTokens + c.CompletionTokens, ct);
}

using Korkut.Agent.Repositories.Core;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Agent.Repositories.Implementations;

/// <summary>EF adapter over the Agent DB <c>llm_calls</c> ledger (RLS-scoped by the surrounding unit of work).</summary>
public sealed class LlmCallRepository(AgentDbContext db) : ILlmCallRepository
{
    public async Task AddAsync(LlmCallEntry entry, CancellationToken ct = default)
    {
        db.LlmCalls.Add(new LlmCallEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = entry.OfficeId,
            Feature = entry.Feature,
            Provider = entry.Provider,
            ModelId = entry.ModelId,
            PromptFingerprint = entry.PromptFingerprint,
            PromptTokens = entry.PromptTokens,
            CompletionTokens = entry.CompletionTokens,
            Cost = entry.Cost,
            LatencyMs = entry.LatencyMs,
            CreatedAt = entry.CreatedAt,
        });
        await db.SaveChangesAsync(ct);
    }

    public Task<long> SumMonthTokensAsync(Guid officeId, DateTimeOffset monthStart, CancellationToken ct = default)
        => db.LlmCalls
            .Where(c => c.OfficeId == officeId && c.CreatedAt >= monthStart)
            .SumAsync(c => (long)c.PromptTokens + c.CompletionTokens, ct);
}

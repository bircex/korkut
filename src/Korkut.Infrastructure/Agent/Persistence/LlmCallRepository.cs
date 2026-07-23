using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Ids;
using Microsoft.EntityFrameworkCore;
using Korkut.Application.Agent.Abstractions;

namespace Korkut.Infrastructure.Agent.Persistence;

/// <summary>EF adapter over the Agent DB <c>llm_calls</c> ledger (RLS-scoped by the surrounding unit of work).</summary>
public sealed class LlmCallRepository(AgentDbContext db) : EfRepository<LlmCallEntity>(db), ILlmCallRepository
{
    public async Task AddAsync(LlmCallEntry entry, CancellationToken ct = default)
    {
        Db.LlmCalls.Add(new LlmCallEntity
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
        await Db.SaveChangesAsync(ct);
    }

    public Task<long> SumMonthTokensAsync(Guid officeId, DateTimeOffset monthStart, CancellationToken ct = default)
        => Db.LlmCalls
            .Where(c => c.OfficeId == officeId && c.CreatedAt >= monthStart)
            .SumAsync(c => (long)c.PromptTokens + c.CompletionTokens, ct);
}

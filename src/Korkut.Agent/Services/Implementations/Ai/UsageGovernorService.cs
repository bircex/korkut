using System.Collections.Concurrent;
using Korkut.Application.Ai;
using Korkut.Agent.Application.Sessions;
using Korkut.Agent.Services.Core.Ai;
using Korkut.Application.Runtime;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Agent.Services.Implementations.Ai;

/// <summary>
/// Holds one <see cref="SemaphoreSlim"/> per office (process-wide singleton), so the concurrency
/// cap is shared across all requests on THIS replica. A distributed cap across replicas would need
/// shared coordination — deferred; per-replica is the first, safe cut.
/// </summary>
public sealed class OfficeConcurrencyGate
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    public SemaphoreSlim For(Guid officeId, int cap) => _gates.GetOrAdd(officeId, _ => new SemaphoreSlim(cap, cap));
}

/// <summary>
/// Enforces the per-office token budget (from the <c>llm_calls</c> ledger, current calendar month)
/// and concurrency cap before a model call. Both are opt-in via config (0 = unlimited). The budget
/// read is tenant-scoped (RLS on the caller's Agent DB context). The returned handle releases the
/// concurrency slot on dispose.
/// </summary>
public sealed class UsageGovernorService(AgentDbContext db, RuntimeConfig config, IClock clock, OfficeConcurrencyGate gate) : IUsageGovernorService
{
    public async Task<IAsyncDisposable> BeginCallAsync(Guid officeId, CancellationToken ct = default)
    {
        var budget = config.OfficeTokenBudget;
        if (budget > 0)
        {
            var now = clock.UtcNow;
            var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var used = await db.LlmCalls
                .Where(c => c.OfficeId == officeId && c.CreatedAt >= monthStart)
                .SumAsync(c => (long)c.PromptTokens + c.CompletionTokens, ct);
            if (used >= budget)
            {
                throw new BudgetExceededException("Aylık yapay zeka jeton bütçesi aşıldı.");
            }
        }

        var cap = config.OfficeConcurrency;
        if (cap <= 0)
        {
            return NoRelease.Instance;
        }

        var semaphore = gate.For(officeId, cap);
        await semaphore.WaitAsync(ct);
        return new Release(semaphore);
    }

    private sealed class Release(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoRelease : IAsyncDisposable
    {
        public static readonly NoRelease Instance = new();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

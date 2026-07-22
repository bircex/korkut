using System.Collections.Concurrent;
using Korkut.Application.Ai;
using Korkut.Application.Runtime;
using Korkut.Domain.Time;
using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;

namespace Korkut.Infrastructure.Agent.Ai;

/// <summary>
/// Holds one <see cref="SemaphoreSlim"/> per office (process-wide singleton), so the concurrency
/// cap is shared across all requests on THIS replica. A distributed cap across replicas would need
/// shared coordination — deferred; per-replica is the first, safe cut.
/// </summary>
public sealed class OfficeConcurrencyGate : IOfficeConcurrencyGate
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _gates = new();

    public SemaphoreSlim For(Guid officeId, int cap) => _gates.GetOrAdd(officeId, _ => new SemaphoreSlim(cap, cap));
}

/// <summary>
/// Enforces the per-office token budget (from the <c>llm_calls</c> ledger, current calendar month)
/// and concurrency cap before a model call. Both are opt-in via config (0 = unlimited). The budget
/// read goes through <see cref="ILlmCallRepository"/> (tenant-scoped by the surrounding unit of work).
/// The returned handle releases the concurrency slot on dispose.
/// </summary>
public sealed class UsageGovernorService(
    ILlmCallRepository calls, RuntimeConfig config, IClock clock, IOfficeConcurrencyGate gate) : IUsageGovernorService
{
    public async Task<IAsyncDisposable> BeginCallAsync(Guid officeId, CancellationToken ct = default)
    {
        var budget = config.OfficeTokenBudget;
        if (budget > 0)
        {
            var now = clock.UtcNow;
            var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var used = await calls.SumMonthTokensAsync(officeId, monthStart, ct);
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

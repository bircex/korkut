using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Abstractions;

/// <summary>One row for the <c>llm_calls</c> ledger (already priced): the seam services persist a model call through.</summary>
public sealed record LlmCallEntry(
    Guid OfficeId, string Feature, string Provider, string ModelId, string PromptFingerprint,
    int PromptTokens, int CompletionTokens, decimal Cost, int LatencyMs, DateTimeOffset CreatedAt);

/// <summary>The Agent DB <c>llm_calls</c> ledger: append a priced call, and sum a month's tokens for the budget.</summary>
public interface ILlmCallRepository
{
    Task AddAsync(LlmCallEntry entry, CancellationToken ct = default);

    Task<long> SumMonthTokensAsync(Guid officeId, DateTimeOffset monthStart, CancellationToken ct = default);
}

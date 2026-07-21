using System.Diagnostics;
using Korkut.Application.Ai;
using Korkut.Infrastructure.Observability;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;

namespace Korkut.Infrastructure.Ai;

/// <summary>
/// Persists each model call to the Agent DB <c>llm_calls</c> ledger and emits a first-class span on
/// the shared "Korkut" <see cref="ActivitySource"/> carrying provider / model / tokens / cost /
/// latency / office / feature (principle 8/9). Writes through the caller's tenant-scoped
/// <see cref="AgentDbContext"/> (RLS), so the row is office-scoped. Cost is derived from
/// <see cref="ModelPricing"/> at record time.
/// </summary>
public sealed class EfLlmCallLog(AgentDbContext db, IClock clock) : ILlmCallLog
{
    private static readonly ActivitySource Source = new(ObservabilitySetup.ActivitySourceName);

    public async Task RecordAsync(
        Guid officeId, string feature, ChatUsage usage, TimeSpan latency, string promptFingerprint, CancellationToken ct = default)
    {
        var cost = ModelPricing.Cost(usage.ModelId, usage.PromptTokens, usage.CompletionTokens);

        using (var span = Source.StartActivity("llm.call"))
        {
            if (span is not null)
            {
                span.SetTag("provider", usage.Provider);
                span.SetTag("model_id", usage.ModelId);
                span.SetTag("prompt_fingerprint", promptFingerprint);
                span.SetTag("prompt_tokens", usage.PromptTokens);
                span.SetTag("completion_tokens", usage.CompletionTokens);
                span.SetTag("cost", (double)cost);
                span.SetTag("latency_ms", latency.TotalMilliseconds);
                span.SetTag("office_id", officeId);
                span.SetTag("feature", feature);
            }
        }

        db.LlmCalls.Add(new LlmCallEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Feature = feature,
            Provider = usage.Provider,
            ModelId = usage.ModelId,
            PromptFingerprint = promptFingerprint,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            Cost = cost,
            LatencyMs = (int)latency.TotalMilliseconds,
            CreatedAt = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}

using Korkut.Contract.Agent.Model;
using Korkut.Agent.Ledger;
using System.Diagnostics;
using Korkut.Core;

namespace Korkut.Agent.Model.Services;

/// <summary>
/// Records each model call to the <c>llm_calls</c> ledger (via <see cref="ILlmCallRepository"/>) and
/// emits a first-class "llm.call" span carrying provider / model / tokens / cost / latency / office /
/// feature (principle 8/9). Cost is derived from <see cref="ModelPricing"/> at record time. No
/// DbContext here — persistence lives behind the repository.
/// </summary>
internal sealed class LlmCallLogService(ILlmCallRepository calls, IClock clock) : ILlmCallLogService
{
    private static readonly ActivitySource Source = new(Korkut.Core.Observability.ActivitySourceName);

    public async Task RecordAsync(
        Guid officeId, Guid? userId, string feature, ChatUsage usage, TimeSpan latency, string promptFingerprint,
        CancellationToken ct = default)
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
                span.SetTag("user_id", userId);
                span.SetTag("feature", feature);
            }
        }

        await calls.AddAsync(new LlmCallEntry(
            officeId, userId, feature, usage.Provider, usage.ModelId, promptFingerprint,
            usage.PromptTokens, usage.CompletionTokens, cost, (int)latency.TotalMilliseconds, clock.UtcNow), ct);
    }
}

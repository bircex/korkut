using Korkut.Domain.Common;

namespace Korkut.Domain.Agent.Ledger;

/// <summary>
/// One recorded model call — the cost/token ledger the per-office budgets and usage
/// aggregations read. Created complete, from figures already known at record time (the cost
/// is derived from the model's pricing before the row exists), and never changed afterwards:
/// a ledger entry is history, so this type carries no mutators.
/// </summary>
public sealed class LlmCall : Entity, ITenantOwned, IAggregateRoot
{
    public LlmCall(
        Guid officeId, string feature, string provider, string modelId, string promptFingerprint,
        int promptTokens, int completionTokens, decimal cost, int latencyMs, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        Feature = feature;
        Provider = provider;
        ModelId = modelId;
        PromptFingerprint = promptFingerprint;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        Cost = cost;
        LatencyMs = latencyMs;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private LlmCall()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>The call site: "chat" | "drafting" | …</summary>
    public string Feature { get; private set; } = string.Empty;

    public string Provider { get; private set; } = string.Empty;

    public string ModelId { get; private set; } = string.Empty;

    /// <summary>Fingerprint of the composed prompt — ties a call to a prompt version.</summary>
    public string PromptFingerprint { get; private set; } = string.Empty;

    public int PromptTokens { get; private set; }

    public int CompletionTokens { get; private set; }

    /// <summary>Derived cost in USD (0 when the model has no known price).</summary>
    public decimal Cost { get; private set; }

    public int LatencyMs { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}

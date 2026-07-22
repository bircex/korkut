namespace Korkut.Infrastructure.Persistence.Entities;

/// <summary>
/// One recorded model call in the Agent DB (schema <c>korkut_agent</c>) — the cost/token ledger the
/// <c>llm_calls</c> aggregations and per-office budgets read. Tenant-scoped by <see cref="OfficeId"/>
/// (RLS). Cost is derived from the provider/model pricing at record time.
/// </summary>
public sealed class LlmCallEntity : ITenantOwned, IIdentifiable
{
    public Guid Id { get; set; }

    public Guid OfficeId { get; set; }

    /// <summary>The call site: "chat" | "drafting" | …</summary>
    public string Feature { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;

    /// <summary>Fingerprint of the composed prompt (principle 9) — ties a call to a prompt version.</summary>
    public string PromptFingerprint { get; set; } = string.Empty;

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    /// <summary>Derived cost in USD (0 when the model has no known price).</summary>
    public decimal Cost { get; set; }

    public int LatencyMs { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

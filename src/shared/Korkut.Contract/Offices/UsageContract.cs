namespace Korkut.Contract.Offices;

/// <summary>Aggregated model usage over some slice of the <c>llm_calls</c> ledger.</summary>
public sealed record AdminUsageTotals(
    long PromptTokens, long CompletionTokens, long TotalTokens, decimal Cost, int Calls)
{
    public static readonly AdminUsageTotals Empty = new(0, 0, 0, 0m, 0);
}

/// <summary>One row of a usage breakdown — a member, a model, a feature, or an office.</summary>
public sealed record AdminUsageRow(string Key, string Label, AdminUsageTotals Totals);

/// <summary>
/// One office's model usage over a window, broken down the three ways the console shows it: who
/// spent it (member), what spent it (model), and where (feature: chat / drafting / …).
/// </summary>
public sealed record AdminOfficeUsageResponse(
    Guid OfficeId, string OfficeName, DateTimeOffset From, DateTimeOffset To, AdminUsageTotals Totals,
    IReadOnlyList<AdminUsageRow> ByMember, IReadOnlyList<AdminUsageRow> ByModel, IReadOnlyList<AdminUsageRow> ByFeature);

/// <summary>Platform-wide model usage over a window, broken down by office and by model.</summary>
public sealed record AdminUsageSummaryResponse(
    DateTimeOffset From, DateTimeOffset To, AdminUsageTotals Totals,
    IReadOnlyList<AdminUsageRow> ByOffice, IReadOnlyList<AdminUsageRow> ByModel);

/// <summary>
/// Read-only reporting over the Agent DB's <c>llm_calls</c> ledger for the admin console. The ledger
/// lives in the <c>korkut_agent</c> schema of the same database, so these reads run as parameterized
/// raw SQL inside the caller's tenant scope — the same posture as the console's other cross-tenant
/// counts. Windows are half-open [from, to).
/// </summary>
public interface IUsageReportService
{
    Task<Result<AdminOfficeUsageResponse>> OfficeUsageAsync(
        Guid officeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);

    Task<Result<AdminUsageSummaryResponse>> SummaryAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default);
}

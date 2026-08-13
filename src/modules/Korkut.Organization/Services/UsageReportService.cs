using System.Data;
using System.Data.Common;
using Korkut.Contract;
using Korkut.Contract.Offices;
using Korkut.Organization.Entities;
using Korkut.Organization.Repositories;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Korkut.Organization.Services;

/// <summary>
/// Token/cost reporting over the Agent DB's <c>llm_calls</c> ledger for the admin console.
///
/// The ledger is OWNED by <c>AgentDbContext</c> (schema <c>korkut_agent</c>), which this host does
/// not register — but both schemas live in the same database and both hosts connect as the same
/// <c>korkut_app</c> role, so a reporting read is a plain cross-schema query on the connection the
/// tenant scope already holds. It is written as explicit ADO rather than mapped onto
/// <c>KorkutDbContext</c> on purpose: this is a read-only projection, and mapping the table here
/// would imply the operational context owns a table it does not. The ledger's RLS still applies —
/// every read runs inside <see cref="Tenancy.BeginTenantScopeAsync"/>, which sets the same
/// <c>app.current_office</c> GUC the agent policies read.
///
/// Windows are half-open [from, to).
/// </summary>
public sealed class UsageReportService(DbContext db, IOfficeRepository offices) : IUsageReportService
{
    private const string LedgerTable = "korkut_agent.llm_calls";

    private const string TotalsProjection = """
        coalesce(sum(prompt_tokens), 0)::bigint AS prompt_tokens,
        coalesce(sum(completion_tokens), 0)::bigint AS completion_tokens,
        coalesce(sum(cost), 0)::numeric AS cost,
        count(*)::int AS calls
        """;

    public async Task<Result<AdminOfficeUsageResponse>> OfficeUsageAsync(
        Guid officeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        var office = await offices.FindAsync(officeId, ct);
        if (office is null)
        {
            return Result<AdminOfficeUsageResponse>.NotFound("Ofis bulunamadı.");
        }

        await using var scope = await db.BeginTenantScopeAsync(officeId, ct);

        var totals = await TotalsAsync(officeId, from, to, ct);
        var byMember = await BreakdownAsync(officeId, from, to, "coalesce(user_id::text, '')", ct);
        var byModel = await BreakdownAsync(officeId, from, to, "provider || ':' || model_id", ct);
        var byFeature = await BreakdownAsync(officeId, from, to, "feature", ct);

        // Member rows are keyed by user id; resolve display names from the same tenant scope.
        var members = await db.Set<User>().AsNoTracking()
            .Where(u => u.OfficeId == officeId)
            .Select(u => new { u.Id, u.Email, u.FullName })
            .ToListAsync(ct);

        await scope.CommitAsync(ct);

        var names = members.ToDictionary(m => m.Id.ToString(), m => m.FullName ?? m.Email);
        var memberRows = byMember
            .Select(r => new AdminUsageRow(
                r.Key,
                r.Key.Length == 0 ? "Atanmamış" : names.GetValueOrDefault(r.Key, "Silinmiş kullanıcı"),
                r.Totals))
            .OrderByDescending(r => r.Totals.TotalTokens)
            .ToList();

        return Result<AdminOfficeUsageResponse>.Ok(new AdminOfficeUsageResponse(
            officeId, office.Name, from, to, totals,
            memberRows,
            byModel.OrderByDescending(r => r.Totals.TotalTokens).ToList(),
            byFeature.OrderByDescending(r => r.Totals.TotalTokens).ToList()));
    }

    public async Task<Result<AdminUsageSummaryResponse>> SummaryAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default)
    {
        // RLS scopes the ledger per office, so the platform-wide view is the per-office reads summed
        // — the same shape as the console's other cross-tenant aggregates.
        var all = await offices.ListAsync(ct);
        var byOffice = new List<AdminUsageRow>(all.Count);
        var byModel = new Dictionary<string, AdminUsageTotals>(StringComparer.Ordinal);
        var grand = AdminUsageTotals.Empty;

        foreach (var office in all)
        {
            await using var scope = await db.BeginTenantScopeAsync(office.Id, ct);
            var totals = await TotalsAsync(office.Id, from, to, ct);
            var models = await BreakdownAsync(office.Id, from, to, "provider || ':' || model_id", ct);
            await scope.CommitAsync(ct);

            if (totals.Calls > 0)
            {
                byOffice.Add(new AdminUsageRow(office.Id.ToString(), office.Name, totals));
            }

            grand = Add(grand, totals);
            foreach (var m in models)
            {
                byModel[m.Key] = Add(byModel.GetValueOrDefault(m.Key, AdminUsageTotals.Empty), m.Totals);
            }
        }

        return Result<AdminUsageSummaryResponse>.Ok(new AdminUsageSummaryResponse(
            from, to, grand,
            byOffice.OrderByDescending(r => r.Totals.TotalTokens).ToList(),
            byModel
                .Select(kv => new AdminUsageRow(kv.Key, kv.Key, kv.Value))
                .OrderByDescending(r => r.Totals.TotalTokens)
                .ToList()));
    }

    private async Task<AdminUsageTotals> TotalsAsync(
        Guid officeId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        await using var command = Command(
            $"SELECT {TotalsProjection} FROM {LedgerTable} WHERE office_id = @office AND created_at >= @from AND created_at < @to",
            officeId, from, to);
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadTotals(reader, offset: 0) : AdminUsageTotals.Empty;
    }

    /// <summary>
    /// One GROUP BY over the ledger. <paramref name="keyExpression"/> is a fixed SQL fragment chosen
    /// from this class's own call sites — never caller input — so it carries no injection surface;
    /// the office and the window stay parameterized.
    /// </summary>
    private async Task<List<AdminUsageRow>> BreakdownAsync(
        Guid officeId, DateTimeOffset from, DateTimeOffset to, string keyExpression, CancellationToken ct)
    {
        await using var command = Command(
            $"""
             SELECT {keyExpression} AS bucket, {TotalsProjection}
             FROM {LedgerTable}
             WHERE office_id = @office AND created_at >= @from AND created_at < @to
             GROUP BY 1
             """,
            officeId, from, to);
        await using var reader = await command.ExecuteReaderAsync(ct);

        var rows = new List<AdminUsageRow>();
        while (await reader.ReadAsync(ct))
        {
            var key = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            rows.Add(new AdminUsageRow(key, key, ReadTotals(reader, offset: 1)));
        }

        return rows;
    }

    private DbCommand Command(string sql, Guid officeId, DateTimeOffset from, DateTimeOffset to)
    {
        var connection = db.Database.GetDbConnection();
        var command = connection.CreateCommand();
        command.CommandText = sql;
        // The tenant scope opened a transaction on this connection; the command must join it or the
        // app.current_office GUC it set (is_local) would not apply and RLS would return nothing.
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        Add(command, "office", DbType.Guid, officeId);
        Add(command, "from", DbType.DateTimeOffset, from);
        Add(command, "to", DbType.DateTimeOffset, to);
        return command;

        static void Add(DbCommand command, string name, DbType type, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = type;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }
    }

    private static AdminUsageTotals ReadTotals(DbDataReader reader, int offset)
    {
        var prompt = reader.GetInt64(offset);
        var completion = reader.GetInt64(offset + 1);
        return new AdminUsageTotals(
            prompt, completion, prompt + completion, reader.GetDecimal(offset + 2), reader.GetInt32(offset + 3));
    }

    private static AdminUsageTotals Add(AdminUsageTotals a, AdminUsageTotals b) => new(
        a.PromptTokens + b.PromptTokens,
        a.CompletionTokens + b.CompletionTokens,
        a.TotalTokens + b.TotalTokens,
        a.Cost + b.Cost,
        a.Calls + b.Calls);
}

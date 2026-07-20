using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Korkut.Api.Errors;
using Korkut.Api.Tenancy;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;

namespace Korkut.Api.Endpoints;

public sealed record InvoiceResponse(
    Guid Id, Guid? ClientId, Guid? MatterId, decimal Amount, decimal VatRate, decimal Gross,
    string? Description, string Status, DateTimeOffset? IssuedAt);

public sealed record InvoiceCreateRequest
{
    [Range(0, 100_000_000)]
    public decimal Amount { get; init; }

    /// <summary>VAT rate as a fraction (0.20 = 20%). Defaults to Turkey's standard 20%.</summary>
    [Range(0, 1)]
    public decimal VatRate { get; init; } = 0.20m;

    public Guid? ClientId { get; init; }

    public Guid? MatterId { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    [RegularExpression("draft|sent|paid|cancelled")]
    public string Status { get; init; } = "draft";

    public DateTimeOffset? IssuedAt { get; init; }
}

public sealed record InvoicePatchRequest
{
    [RegularExpression("draft|sent|paid|cancelled")]
    public string? Status { get; init; }

    [Range(0, 100_000_000)]
    public decimal? Amount { get; init; }
}

public sealed record ExpenseResponse(
    Guid Id, Guid? MatterId, Guid? ClientId, decimal Amount, string? Kind, string? Description, DateTimeOffset? SpentAt);

public sealed record ExpenseCreateRequest
{
    [Range(0, 100_000_000)]
    public decimal Amount { get; init; }

    public Guid? MatterId { get; init; }

    public Guid? ClientId { get; init; }

    [MaxLength(50)]
    public string? Kind { get; init; }

    [MaxLength(1000)]
    public string? Description { get; init; }

    public DateTimeOffset? SpentAt { get; init; }
}

public sealed record FinanceSummaryResponse(decimal Invoiced, decimal Paid, decimal Outstanding, decimal Expenses);

/// <summary>
/// Finance endpoints (invoices, expenses, office money summary) on the .NET runtime.
/// Authenticated, tenant-scoped (RLS), DTO-mapped. Reads use billing.read, writes
/// billing.write. Money is exact decimal; the invoice gross is derived, not stored.
/// </summary>
public static class FinanceEndpoints
{
    public static IEndpointRouteBuilder MapFinanceEndpoints(this IEndpointRouteBuilder app)
    {
        var finance = app.MapGroup("/api/finance");

        // --- Invoices ---
        finance.MapGet("/invoices", async (ClaimsPrincipal user, KorkutDbContext db, FinanceStore store, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o => (await store.ListInvoicesAsync(o, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.BillingRead);

        finance.MapPost("/invoices", async (InvoiceCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, FinanceStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var created = await user.InTenantScopeAsync(db, async o =>
                {
                    var id = await store.CreateInvoiceAsync(o, body.Amount, body.VatRate, body.ClientId, body.MatterId, body.Description?.Trim(), body.Status, body.IssuedAt, ct);
                    return await store.GetInvoiceAsync(o, id, ct);
                }, ct);
                return Results.Created($"/api/finance/invoices/{created!.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.BillingWrite);

        finance.MapPatch("/invoices/{id:guid}", async (Guid id, InvoicePatchRequest body, ClaimsPrincipal user, KorkutDbContext db, FinanceStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var updated = await user.InTenantScopeAsync(db, async o =>
                {
                    var ok = await store.UpdateInvoiceAsync(o, id, body.Status, body.Amount, ct);
                    return ok ? await store.GetInvoiceAsync(o, id, ct) : null;
                }, ct);
                return updated is null ? throw ApiException.NotFound("Fatura bulunamadı.") : Results.Ok(ToDto(updated));
            })
            .RequireAuthorization(Permissions.BillingWrite);

        finance.MapDelete("/invoices/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, FinanceStore store, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => store.DeleteInvoiceAsync(o, id, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Fatura bulunamadı.");
            })
            .RequireAuthorization(Permissions.BillingWrite);

        // --- Expenses ---
        finance.MapGet("/expenses", async (ClaimsPrincipal user, KorkutDbContext db, FinanceStore store, CancellationToken ct) =>
            Results.Ok(await user.InTenantScopeAsync(db, async o => (await store.ListExpensesAsync(o, ct)).Select(ToDto).ToList(), ct)))
            .RequireAuthorization(Permissions.BillingRead);

        finance.MapPost("/expenses", async (ExpenseCreateRequest body, ClaimsPrincipal user, KorkutDbContext db, FinanceStore store, CancellationToken ct) =>
            {
                var invalid = DtoValidation.Validate(body);
                if (invalid is not null)
                {
                    return invalid;
                }

                var created = await user.InTenantScopeAsync(db, async o =>
                {
                    var id = await store.CreateExpenseAsync(o, body.Amount, body.MatterId, body.ClientId, body.Kind?.Trim(), body.Description?.Trim(), body.SpentAt, ct);
                    return (await store.ListExpensesAsync(o, ct)).First(x => x.Id == id);
                }, ct);
                return Results.Created($"/api/finance/expenses/{created.Id}", ToDto(created));
            })
            .RequireAuthorization(Permissions.BillingWrite);

        finance.MapDelete("/expenses/{id:guid}", async (Guid id, ClaimsPrincipal user, KorkutDbContext db, FinanceStore store, CancellationToken ct) =>
            {
                var ok = await user.InTenantScopeAsync(db, o => store.DeleteExpenseAsync(o, id, ct), ct);
                return ok ? Results.NoContent() : throw ApiException.NotFound("Masraf bulunamadı.");
            })
            .RequireAuthorization(Permissions.BillingWrite);

        // --- Summary ---
        finance.MapGet("/summary", async (ClaimsPrincipal user, KorkutDbContext db, FinanceStore store, CancellationToken ct) =>
            {
                var t = await user.InTenantScopeAsync(db, o => store.SummaryAsync(o, ct), ct);
                return Results.Ok(new FinanceSummaryResponse(t.Invoiced, t.Paid, t.Outstanding, t.Expenses));
            })
            .RequireAuthorization(Permissions.BillingRead);

        return app;
    }

    private static InvoiceResponse ToDto(InvoiceEntity i) =>
        new(i.Id, i.ClientId, i.MatterId, i.Amount, i.VatRate, FinanceStore.Gross(i.Amount, i.VatRate), i.Description, i.Status, i.IssuedAt);

    private static ExpenseResponse ToDto(ExpenseEntity e) =>
        new(e.Id, e.MatterId, e.ClientId, e.Amount, e.Kind, e.Description, e.SpentAt);
}

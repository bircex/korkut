using Korkut.Domain.Finance;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for invoices. Implemented in Infrastructure over EF/Postgres; the
/// caller has already pinned the tenant scope (RLS), and every method still filters by
/// <paramref name="officeId"/> as defense in depth. Speaks in DOMAIN models, never EF
/// entities — that boundary is what keeps the Application layer persistence-agnostic.
/// </summary>
public interface IInvoiceRepository
{
    Task<Invoice> AddAsync(Invoice invoice, CancellationToken ct = default);

    Task<IReadOnlyList<Invoice>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<Invoice?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default);

    /// <summary>Patch status and/or net amount; null leaves a field unchanged. False if missing.</summary>
    Task<bool> UpdateAsync(Guid officeId, Guid id, string? status, decimal? amount, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);
}

/// <summary>Persistence port for expenses (see <see cref="IInvoiceRepository"/> for the contract shape).</summary>
public interface IExpenseRepository
{
    Task<Expense> AddAsync(Expense expense, CancellationToken ct = default);

    Task<IReadOnlyList<Expense>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);
}

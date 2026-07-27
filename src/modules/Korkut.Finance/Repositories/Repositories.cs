using Korkut.Core;
using Korkut.Finance.Entities;
using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Finance.Repositories;

/// <summary>Persistence port for invoices; listings are most-recently-issued first.</summary>
internal interface IInvoiceRepository : IRepository<Invoice>;

/// <summary>Persistence port for expenses; listings are most-recently-spent first.</summary>
internal interface IExpenseRepository : IRepository<Expense>;

/// <summary>EF/Postgres adapter for <see cref="IInvoiceRepository"/> over the ambient DbContext.</summary>
internal sealed class InvoiceRepository(DbContext db) : Repository<Invoice>(db), IInvoiceRepository
{
    public override async Task<IReadOnlyList<Invoice>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(x => x.IssuedAt ?? x.CreatedAt).ToListAsync(ct);
}

/// <summary>EF/Postgres adapter for <see cref="IExpenseRepository"/> over the ambient DbContext.</summary>
internal sealed class ExpenseRepository(DbContext db) : Repository<Expense>(db), IExpenseRepository
{
    public override async Task<IReadOnlyList<Expense>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking().OrderByDescending(x => x.SpentAt ?? x.CreatedAt).ToListAsync(ct);
}

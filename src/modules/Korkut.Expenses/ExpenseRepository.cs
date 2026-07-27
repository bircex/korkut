using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Expenses;

/// <summary>EF/Postgres adapter for <see cref="IExpenseRepository"/> over the ambient DbContext.</summary>
public sealed class ExpenseRepository(DbContext db) : Repository<Expense>(db), IExpenseRepository
{
    public override async Task<IReadOnlyList<Expense>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .OrderByDescending(x => x.SpentAt ?? x.CreatedAt).ToListAsync(ct);
}

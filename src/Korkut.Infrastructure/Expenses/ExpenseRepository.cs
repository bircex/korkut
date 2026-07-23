using Korkut.Application.Expenses;
using Korkut.Domain.Expenses;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Expenses;

/// <summary>EF/Postgres adapter for <see cref="IExpenseRepository"/>.</summary>
public sealed class ExpenseRepository(KorkutDbContext db) : Repository<Expense>(db), IExpenseRepository
{
    public override async Task<IReadOnlyList<Expense>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .OrderByDescending(x => x.SpentAt ?? x.CreatedAt).ToListAsync(ct);
}

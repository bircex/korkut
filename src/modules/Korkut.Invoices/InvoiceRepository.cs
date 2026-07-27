using Korkut.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Invoices;

/// <summary>EF/Postgres adapter for <see cref="IInvoiceRepository"/> over the ambient DbContext.</summary>
public sealed class InvoiceRepository(DbContext db) : Repository<Invoice>(db), IInvoiceRepository
{
    public override async Task<IReadOnlyList<Invoice>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .OrderByDescending(x => x.IssuedAt ?? x.CreatedAt).ToListAsync(ct);
}

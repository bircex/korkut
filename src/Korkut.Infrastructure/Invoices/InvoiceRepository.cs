using Korkut.Application.Invoices;
using Korkut.Domain.Invoices;
using Korkut.Infrastructure.Common;
using Korkut.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Invoices;

/// <summary>EF/Postgres adapter for <see cref="IInvoiceRepository"/>.</summary>
public sealed class InvoiceRepository(KorkutDbContext db) : Repository<Invoice>(db), IInvoiceRepository
{
    public override async Task<IReadOnlyList<Invoice>> ListAsync(Guid officeId, CancellationToken ct = default)
        => await Scoped(officeId).AsNoTracking()
            .OrderByDescending(x => x.IssuedAt ?? x.CreatedAt).ToListAsync(ct);
}

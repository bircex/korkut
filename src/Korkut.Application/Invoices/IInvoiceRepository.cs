using Korkut.Application.Common;
using Korkut.Domain.Invoices;

namespace Korkut.Application.Invoices;

/// <summary>Persistence port for invoices; listings are most-recently-issued first.</summary>
public interface IInvoiceRepository : IRepository<Invoice>;

using Korkut.Core;

namespace Korkut.Invoices;

/// <summary>Persistence port for invoices; listings are most-recently-issued first.</summary>
public interface IInvoiceRepository : IRepository<Invoice>;

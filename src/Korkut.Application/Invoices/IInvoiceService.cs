using Korkut.Application.Invoices.Requests;
using Korkut.Application.Invoices.Responses;

namespace Korkut.Application.Invoices;

/// <summary>Invoice use-cases. Missing records raise <c>NotFoundException</c>.</summary>
public interface IInvoiceService
{
    Task<IReadOnlyList<InvoiceResponse>> ListAsync(CancellationToken ct = default);

    Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken ct = default);

    Task<InvoiceResponse> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

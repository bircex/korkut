namespace Korkut.Contract.Invoices;

/// <summary>Invoice use-cases (cross-module contract). Calls RETURN a <see cref="Result"/>.</summary>
public interface IInvoiceService
{
    Task<Result<IReadOnlyList<InvoiceResponse>>> ListAsync(CancellationToken ct = default);

    Task<Result<InvoiceResponse>> CreateAsync(CreateInvoiceRequest request, CancellationToken ct = default);

    Task<Result<InvoiceResponse>> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

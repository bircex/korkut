using Korkut.Core;
using Korkut.Application.Errors;
using Korkut.Application.Invoices.Requests;
using Korkut.Application.Invoices.Responses;
using Korkut.Domain.Invoices;

namespace Korkut.Application.Invoices;

/// <summary>Invoice use-cases over the repository port; an omitted issue date defaults to now.</summary>
public sealed class InvoiceService(
    IInvoiceRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IInvoiceService
{
    public Task<IReadOnlyList<InvoiceResponse>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<InvoiceResponse>)(await repository.ListAsync(office, ct)).Select(ToResponse).ToList(), ct);

    public Task<InvoiceResponse> CreateAsync(CreateInvoiceRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var now = clock.UtcNow;
            var invoice = new Invoice(
                office, request.Amount, request.VatRate, request.ClientId, request.MatterId,
                request.Description, request.Status, request.IssuedAt ?? now, now);
            await repository.AddAsync(invoice, ct);
            return ToResponse(invoice);
        }, ct);

    public Task<InvoiceResponse> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var invoice = await repository.FindForUpdateAsync(office, id, ct)
                ?? throw new NotFoundException("Fatura bulunamadı.");
            invoice.Update(request.Status, request.Amount);
            await repository.SaveChangesAsync(ct);
            return ToResponse(invoice);
        }, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (!await repository.RemoveAsync(office, id, ct))
            {
                throw new NotFoundException("Fatura bulunamadı.");
            }

            return true;
        }, ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static InvoiceResponse ToResponse(Invoice i) =>
        new(i.Id, i.ClientId, i.MatterId, i.Amount, i.VatRate, i.Gross, i.Description, i.Status, i.IssuedAt);
}

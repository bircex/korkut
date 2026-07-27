using Korkut.Contract;
using Korkut.Contract.Invoices;
using Korkut.Core;

namespace Korkut.Invoices;

/// <summary>Invoice use-cases over the repository port; an omitted issue date defaults to now. Returns Result.</summary>
public sealed class InvoiceService(
    IInvoiceRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IInvoiceService
{
    public Task<Result<IReadOnlyList<InvoiceResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<InvoiceResponse>>.Ok((await repository.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<InvoiceResponse>> CreateAsync(CreateInvoiceRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var now = clock.UtcNow;
            var invoice = new Invoice(
                office, request.Amount, request.VatRate, request.ClientId, request.MatterId,
                request.Description, request.Status, request.IssuedAt ?? now, now);
            await repository.AddAsync(invoice, ct);
            return Result<InvoiceResponse>.Ok(ToResponse(invoice));
        }, ct);

    public Task<Result<InvoiceResponse>> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var invoice = await repository.FindForUpdateAsync(office, id, ct);
            if (invoice is null)
            {
                return Result<InvoiceResponse>.NotFound("Fatura bulunamadı.");
            }

            invoice.Update(request.Status, request.Amount);
            await repository.SaveChangesAsync(ct);
            return Result<InvoiceResponse>.Ok(ToResponse(invoice));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await repository.RemoveAsync(office, id, ct) ? Result.Ok() : Result.NotFound("Fatura bulunamadı."), ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static InvoiceResponse ToResponse(Invoice i) =>
        new(i.Id, i.ClientId, i.MatterId, i.Amount, i.VatRate, i.Gross, i.Description, i.Status, i.IssuedAt);
}

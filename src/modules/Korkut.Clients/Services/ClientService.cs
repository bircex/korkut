using Korkut.Clients.Repositories;
using Korkut.Clients.Entities;
using Korkut.Contract;
using Korkut.Contract.Clients;
using Korkut.Core;

namespace Korkut.Clients.Services;

/// <summary>
/// Client use-cases over the repository port. Every call runs inside the caller's tenant scope and
/// RETURNS a <see cref="Result"/> — a missing record is a NotFound result, not an exception — so the
/// controller maps outcome to HTTP uniformly and the flow stays explicit.
/// </summary>
internal sealed class ClientService(
    IClientRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IClientService
{
    public Task<Result<IReadOnlyList<ClientResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            Result<IReadOnlyList<ClientResponse>>.Ok((await repository.ListAsync(office, ct)).Select(ToResponse).ToList()), ct);

    public Task<Result<ClientResponse>> GetAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var client = await repository.FindAsync(office, id, ct);
            return client is null ? Result<ClientResponse>.NotFound("Müvekkil bulunamadı.") : Result<ClientResponse>.Ok(ToResponse(client));
        }, ct);

    public Task<Result<ClientResponse>> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var client = new Client(
                office, request.FullName!.Trim(), request.Kind, request.Email, request.Phone,
                Normalize(request.NationalId), Normalize(request.Address), request.DateOfBirth, clock.UtcNow);
            await repository.AddAsync(client, ct);
            return Result<ClientResponse>.Ok(ToResponse(client));
        }, ct);

    public Task<Result<ClientResponse>> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var client = await repository.FindForUpdateAsync(office, id, ct);
            if (client is null)
            {
                return Result<ClientResponse>.NotFound("Müvekkil bulunamadı.");
            }

            client.Update(
                request.FullName!.Trim(), request.Kind, request.Email, request.Phone,
                Normalize(request.NationalId), Normalize(request.Address), request.DateOfBirth);
            await repository.SaveChangesAsync(ct);
            return Result<ClientResponse>.Ok(ToResponse(client));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
            await repository.SoftDeleteAsync(office, id, clock.UtcNow, ct)
                ? Result.Ok()
                : Result.NotFound("Müvekkil bulunamadı."), ct);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ClientResponse ToResponse(Client c) =>
        new(c.Id, c.FullName, c.Kind, c.Email, c.Phone, c.NationalId, c.Address, c.DateOfBirth, c.CreatedAt);
}

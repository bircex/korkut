using Korkut.Application.Common;
using Korkut.Application.Errors;
using Korkut.Application.OfficeCore.Clients.Requests;
using Korkut.Application.OfficeCore.Clients.Responses;
using Korkut.Domain.OfficeCore.Clients;
using Korkut.Domain.Time;

namespace Korkut.Application.OfficeCore.Clients;

/// <summary>
/// Client use-cases over the repository port: every call runs inside the caller's tenant
/// scope and returns response DTOs, so the controller stays a pure transport.
/// </summary>
public sealed class ClientService(
    IClientRepository repository, IUnitOfWork unitOfWork, ICurrentUser currentUser, IClock clock) : IClientService
{
    public Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
            (IReadOnlyList<ClientResponse>)(await repository.ListAsync(office, ct)).Select(ToResponse).ToList(), ct);

    public Task<ClientResponse> GetAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office => ToResponse(await FindAsync(office, id, ct)), ct);

    public Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var client = new Client(office, request.Name!.Trim(), request.Kind, request.Email, request.Phone, clock.UtcNow);
            await repository.AddAsync(client, ct);
            return ToResponse(client);
        }, ct);

    public Task<ClientResponse> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var client = await repository.FindForUpdateAsync(office, id, ct)
                ?? throw new NotFoundException("Müvekkil bulunamadı.");
            client.Update(request.Name!.Trim(), request.Kind, request.Email, request.Phone);
            await repository.SaveChangesAsync(ct);
            return ToResponse(client);
        }, ct);

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            if (!await repository.SoftDeleteAsync(office, id, clock.UtcNow, ct))
            {
                throw new NotFoundException("Müvekkil bulunamadı.");
            }

            return true;
        }, ct);

    private async Task<Client> FindAsync(Guid officeId, Guid id, CancellationToken ct)
        => await repository.FindAsync(officeId, id, ct) ?? throw new NotFoundException("Müvekkil bulunamadı.");

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static ClientResponse ToResponse(Client c) =>
        new(c.Id, c.Name, c.Kind, c.Email, c.Phone, c.CreatedAt);
}

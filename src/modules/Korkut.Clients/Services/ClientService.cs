using Korkut.Clients.Repositories;
using Korkut.Clients.Entities;
using Korkut.Contract;
using Korkut.Contract.Clients;
using Korkut.Contract.Matters;
using Korkut.Core;

namespace Korkut.Clients.Services;

/// <summary>
/// Client use-cases over the repository port. Every call runs inside the caller's tenant scope and
/// RETURNS a <see cref="Result"/> — a missing record is a NotFound result, not an exception — so the
/// controller maps outcome to HTTP uniformly and the flow stays explicit.
/// A caller sees every client with clients.view_all; otherwise the clients of the matters in their
/// scope and the ones they added. Contact and identity details are left out without the permission
/// to see them, and an update never overwrites details the caller cannot see.
/// </summary>
internal sealed class ClientService(
    IClientRepository repository, IMatterAccess access, IMatterLookup matters, IUnitOfWork unitOfWork,
    ICurrentUser currentUser, IClock clock) : IClientService
{
    private const string NotFound = "Müvekkil bulunamadı.";

    public Task<Result<IReadOnlyList<ClientResponse>>> ListAsync(CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var visible = await VisibilityAsync(office, ct);
            return Result<IReadOnlyList<ClientResponse>>.Ok(
                (await repository.ListAsync(office, ct)).Where(visible).Select(ToResponse).ToList());
        }, ct);

    public Task<Result<ClientResponse>> GetAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var client = await repository.FindAsync(office, id, ct);
            return client is null || !(await VisibilityAsync(office, ct))(client)
                ? Result<ClientResponse>.NotFound(NotFound)
                : Result<ClientResponse>.Ok(ToResponse(client));
        }, ct);

    public Task<Result<ClientResponse>> CreateAsync(CreateClientRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var client = new Client(
                office, request.FullName!.Trim(), request.Kind, request.Email, request.Phone,
                Normalize(request.NationalId), Normalize(request.Address), request.DateOfBirth, clock.UtcNow, currentUser.UserId);
            await repository.AddAsync(client, ct);
            return Result<ClientResponse>.Ok(ToResponse(client));
        }, ct);

    public Task<Result<ClientResponse>> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var client = await repository.FindForUpdateAsync(office, id, ct);
            if (client is null || !(await VisibilityAsync(office, ct))(client))
            {
                return Result<ClientResponse>.NotFound(NotFound);
            }

            // Details the caller cannot see stay as they are: the form never showed them.
            var contact = Can(Permissions.ClientsContact);
            var identity = Can(Permissions.ClientsIdentity);
            client.Update(
                request.FullName!.Trim(), request.Kind,
                contact ? request.Email : client.Email,
                contact ? request.Phone : client.Phone,
                identity ? Normalize(request.NationalId) : client.NationalId,
                contact ? Normalize(request.Address) : client.Address,
                identity ? request.DateOfBirth : client.DateOfBirth);
            await repository.SaveChangesAsync(ct);
            return Result<ClientResponse>.Ok(ToResponse(client));
        }, ct);

    public Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
        => InScopeAsync(async office =>
        {
            var client = await repository.FindAsync(office, id, ct);
            if (client is null || !(await VisibilityAsync(office, ct))(client))
            {
                return Result.NotFound(NotFound);
            }

            return await repository.SoftDeleteAsync(office, id, clock.UtcNow, ct) ? Result.Ok() : Result.NotFound(NotFound);
        }, ct);

    /// <summary>Which clients the caller may see: all, or their matters' clients and the ones they added.</summary>
    private async Task<Func<Client, bool>> VisibilityAsync(Guid office, CancellationToken ct)
    {
        if (Can(Permissions.ClientsViewAll))
        {
            return _ => true;
        }

        var scope = await access.ScopeAsync(office, ct);
        var theirs = (await matters.ClientLinksAsync(office, ct)).Where(l => scope.CanSee(l.MatterId)).Select(l => l.ClientId).ToHashSet();
        return c => theirs.Contains(c.Id) || c.CreatedBy == currentUser.UserId;
    }

    private bool Can(string permission) => currentUser.Permissions.Contains(permission);

    private Task<T> InScopeAsync<T>(Func<Guid, Task<T>> work, CancellationToken ct)
    {
        var office = currentUser.OfficeId;
        return unitOfWork.InTenantScopeAsync(office, () => work(office), ct);
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private ClientResponse ToResponse(Client c) =>
        new ClientResponse(c.Id, c.FullName, c.Kind, c.Email, c.Phone, c.NationalId, c.Address, c.DateOfBirth, c.CreatedAt)
            .Masked(Can(Permissions.ClientsContact), Can(Permissions.ClientsIdentity));
}

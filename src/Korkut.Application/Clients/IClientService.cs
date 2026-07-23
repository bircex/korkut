using Korkut.Application.Clients.Requests;
using Korkut.Application.Clients.Responses;

namespace Korkut.Application.Clients;

/// <summary>
/// Client (müvekkil) use-cases. The office comes from the caller's token, so no operation
/// takes a tenant argument. Missing records raise <c>NotFoundException</c>.
/// </summary>
public interface IClientService
{
    Task<IReadOnlyList<ClientResponse>> ListAsync(CancellationToken ct = default);

    Task<ClientResponse> GetAsync(Guid id, CancellationToken ct = default);

    Task<ClientResponse> CreateAsync(CreateClientRequest request, CancellationToken ct = default);

    Task<ClientResponse> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

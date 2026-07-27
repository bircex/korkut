namespace Korkut.Contract.Clients;

/// <summary>
/// Client (müvekkil) use-cases — the cross-module contract. The office comes from the caller's
/// token, so no operation takes a tenant argument. Every call RETURNS a <see cref="Result"/>:
/// a missing record is <c>Result&lt;T&gt;.NotFound(...)</c>, never a thrown exception.
/// </summary>
public interface IClientService
{
    Task<Result<IReadOnlyList<ClientResponse>>> ListAsync(CancellationToken ct = default);

    Task<Result<ClientResponse>> GetAsync(Guid id, CancellationToken ct = default);

    Task<Result<ClientResponse>> CreateAsync(CreateClientRequest request, CancellationToken ct = default);

    Task<Result<ClientResponse>> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);
}

using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.OfficeCore;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;

namespace Korkut.Application.Features.OfficeCore;

/// <summary>Client (müvekkil) use-cases.</summary>
public interface IClientService
{
    Task<Client> CreateAsync(Guid officeId, string name, string kind, string? email, string? phone, CancellationToken ct = default);

    Task<IReadOnlyList<Client>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<Client?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<Client?> UpdateAsync(Guid officeId, Guid id, string name, string kind, string? email, string? phone, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);
}

/// <summary>Client orchestration over the repository port.</summary>
public sealed class ClientService(IClientRepository repo, IClock clock) : IClientService
{
    public Task<Client> CreateAsync(Guid officeId, string name, string kind, string? email, string? phone, CancellationToken ct = default)
        => repo.AddAsync(new Client(UuidV7.NewId(), officeId, name, kind, email, phone, null, clock.UtcNow), ct);

    public Task<IReadOnlyList<Client>> ListAsync(Guid officeId, CancellationToken ct = default)
        => repo.ListAsync(officeId, ct);

    public Task<Client?> GetAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => repo.GetAsync(officeId, id, ct);

    public async Task<Client?> UpdateAsync(Guid officeId, Guid id, string name, string kind, string? email, string? phone, CancellationToken ct = default)
        => await repo.UpdateAsync(officeId, id, name, kind, email, phone, ct) ? await repo.GetAsync(officeId, id, ct) : null;

    public Task<bool> DeleteAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => repo.SoftDeleteAsync(officeId, id, clock.UtcNow, ct);
}

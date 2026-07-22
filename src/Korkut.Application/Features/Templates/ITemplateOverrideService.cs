using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Templates;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.Templates;

/// <summary>Template-override use-cases (office + user layers over the built-in catalog).</summary>
public interface ITemplateOverrideService
{
    Task<IReadOnlyList<TemplateOverride>> ListAsync(Guid officeId, string kind, CancellationToken ct = default);

    Task<string?> GetContentAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, CancellationToken ct = default);

    Task SaveAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, string content, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, CancellationToken ct = default);
}

/// <summary>Template-override orchestration over the repository port.</summary>
public sealed class TemplateOverrideService(ITemplateOverrideRepository repo, IClock clock) : ITemplateOverrideService
{
    public Task<IReadOnlyList<TemplateOverride>> ListAsync(Guid officeId, string kind, CancellationToken ct = default)
        => repo.ListAsync(officeId, kind, ct);

    public Task<string?> GetContentAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, CancellationToken ct = default)
        => repo.GetContentAsync(officeId, scope, userId, kind, typeId, ct);

    public Task SaveAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, string content, CancellationToken ct = default)
        => repo.SaveAsync(officeId, scope, userId, kind, typeId, content, clock.UtcNow, ct);

    public Task<bool> DeleteAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, CancellationToken ct = default)
        => repo.DeleteAsync(officeId, scope, userId, kind, typeId, ct);
}

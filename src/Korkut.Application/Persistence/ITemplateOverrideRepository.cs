using Korkut.Domain.Templates;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>Persistence port for per-office/user template overrides (built-in defaults live in the catalog).</summary>
public interface ITemplateOverrideRepository
{
    /// <summary>All overrides for the office of a kind (both scopes) — for has-override flags in a listing.</summary>
    Task<IReadOnlyList<TemplateOverride>> ListAsync(Guid officeId, string kind, CancellationToken ct = default);

    /// <summary>The override content at one layer, or null if none.</summary>
    Task<string?> GetContentAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, CancellationToken ct = default);

    Task SaveAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, string content, DateTimeOffset updatedAt, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid officeId, string scope, Guid userId, string kind, string typeId, CancellationToken ct = default);
}

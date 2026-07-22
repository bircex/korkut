using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Settings;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.Settings;

/// <summary>
/// Office-settings use-cases: AI persona, drafting style (office + user layers), and the
/// office memory list. Timestamps come from the clock here, so callers pass only intent.
/// </summary>
public interface IOfficeSettingsService
{
    Task<Persona?> GetPersonaAsync(Guid officeId, CancellationToken ct = default);

    Task<Persona> UpsertPersonaAsync(Guid officeId, string tone, string expertise, string extraRules, CancellationToken ct = default);

    Task<StyleFields> GetStyleAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default);

    Task<StyleFields> SaveStyleAsync(Guid officeId, string scope, Guid userId, StyleFields fields, CancellationToken ct = default);

    Task<IReadOnlyList<MemoryItem>> ListMemoryAsync(Guid officeId, CancellationToken ct = default);

    Task<MemoryItem> AddMemoryAsync(Guid officeId, string text, string kind, Guid authorId, bool active, CancellationToken ct = default);

    Task<MemoryItem?> ApproveMemoryAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<bool> DeleteMemoryAsync(Guid officeId, Guid id, CancellationToken ct = default);
}

/// <summary>Office-settings orchestration over the repository port.</summary>
public sealed class OfficeSettingsService(IOfficeSettingsRepository repo, IClock clock) : IOfficeSettingsService
{
    public Task<Persona?> GetPersonaAsync(Guid officeId, CancellationToken ct = default)
        => repo.GetPersonaAsync(officeId, ct);

    public Task<Persona> UpsertPersonaAsync(Guid officeId, string tone, string expertise, string extraRules, CancellationToken ct = default)
        => repo.UpsertPersonaAsync(officeId, tone, expertise, extraRules, clock.UtcNow, ct);

    public Task<StyleFields> GetStyleAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default)
        => repo.GetStyleAsync(officeId, scope, userId, ct);

    public async Task<StyleFields> SaveStyleAsync(Guid officeId, string scope, Guid userId, StyleFields fields, CancellationToken ct = default)
    {
        await repo.SaveStyleAsync(officeId, scope, userId, fields, clock.UtcNow, ct);
        return fields;
    }

    public Task<IReadOnlyList<MemoryItem>> ListMemoryAsync(Guid officeId, CancellationToken ct = default)
        => repo.ListMemoryAsync(officeId, ct);

    public Task<MemoryItem> AddMemoryAsync(Guid officeId, string text, string kind, Guid authorId, bool active, CancellationToken ct = default)
        => repo.AddMemoryAsync(officeId, text, kind, authorId, active, clock.UtcNow, ct);

    public Task<MemoryItem?> ApproveMemoryAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => repo.ApproveMemoryAsync(officeId, id, ct);

    public Task<bool> DeleteMemoryAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => repo.DeleteMemoryAsync(officeId, id, ct);
}

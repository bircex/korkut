using Korkut.Domain.Settings;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for office settings: the AI persona (office singleton), drafting
/// style layers (office + per-user), and the office memory list. Persona/style upserts
/// are idempotent by their natural key.
/// </summary>
public interface IOfficeSettingsRepository
{
    Task<Persona?> GetPersonaAsync(Guid officeId, CancellationToken ct = default);

    Task<Persona> UpsertPersonaAsync(Guid officeId, string tone, string expertise, string extraRules, DateTimeOffset updatedAt, CancellationToken ct = default);

    Task<StyleFields> GetStyleAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default);

    Task SaveStyleAsync(Guid officeId, string scope, Guid userId, StyleFields fields, DateTimeOffset updatedAt, CancellationToken ct = default);

    /// <summary>Memory items newest first, authors resolved to emails.</summary>
    Task<IReadOnlyList<MemoryItem>> ListMemoryAsync(Guid officeId, CancellationToken ct = default);

    Task<MemoryItem> AddMemoryAsync(Guid officeId, string text, string kind, Guid authorId, bool active, DateTimeOffset createdAt, CancellationToken ct = default);

    /// <summary>Promote a pending item to active; null if it doesn't exist.</summary>
    Task<MemoryItem?> ApproveMemoryAsync(Guid officeId, Guid id, CancellationToken ct = default);

    Task<bool> DeleteMemoryAsync(Guid officeId, Guid id, CancellationToken ct = default);
}

using Korkut.Domain.Channels;

namespace Korkut.Application.Abstractions.Persistence;

/// <summary>
/// Persistence port for messaging channels. Each office has at most one row per kind;
/// writes upsert it. Provider secrets are encrypted at rest by the adapter and never
/// surfaced (only <see cref="Channel.HasSecret"/>).
/// </summary>
public interface IChannelRepository
{
    Task<Channel?> GetAsync(Guid officeId, string kind, CancellationToken ct = default);

    Task<IReadOnlyList<Channel>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<Channel> SaveEmailAsync(Guid officeId, bool enabled, string? fromAddress, string? fromName, SecretUpdate appPassword, DateTimeOffset updatedAt, CancellationToken ct = default);

    Task<Channel> SaveTelegramAsync(Guid officeId, bool enabled, SecretUpdate botToken, DateTimeOffset updatedAt, CancellationToken ct = default);

    Task<Channel> SaveWhatsAppAsync(Guid officeId, bool enabled, string? baseUrl, SecretUpdate token, DateTimeOffset updatedAt, CancellationToken ct = default);

    /// <summary>Set a fresh one-time pairing code for a kind (unlinks any prior pairing).</summary>
    Task SetLinkCodeAsync(Guid officeId, string kind, string code, DateTimeOffset updatedAt, CancellationToken ct = default);

    /// <summary>Remove the pairing (code + linked state) for a kind. False if there was no row.</summary>
    Task<bool> DeleteLinkAsync(Guid officeId, string kind, DateTimeOffset updatedAt, CancellationToken ct = default);
}

using System.Security.Cryptography;
using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Channels;
using Korkut.Domain.Time;

namespace Korkut.Application.Features.Channels;

/// <summary>
/// Messaging-channel use-cases: per-kind configuration (email/telegram/whatsapp) and
/// external-account pairing. Timestamps and the one-time pairing code are minted here.
/// </summary>
public interface IChannelService
{
    Task<Channel?> GetAsync(Guid officeId, string kind, CancellationToken ct = default);

    Task<IReadOnlyList<Channel>> ListAsync(Guid officeId, CancellationToken ct = default);

    Task<Channel> SaveEmailAsync(Guid officeId, bool enabled, string? fromAddress, string? fromName, SecretUpdate appPassword, CancellationToken ct = default);

    Task<Channel> SaveTelegramAsync(Guid officeId, bool enabled, SecretUpdate botToken, CancellationToken ct = default);

    Task<Channel> SaveWhatsAppAsync(Guid officeId, bool enabled, string? baseUrl, SecretUpdate token, CancellationToken ct = default);

    /// <summary>Mint and store a fresh one-time pairing code for a kind; returns the code.</summary>
    Task<string> CreateLinkAsync(Guid officeId, string kind, CancellationToken ct = default);

    Task<bool> DeleteLinkAsync(Guid officeId, string kind, CancellationToken ct = default);
}

/// <summary>Channel orchestration over the repository port.</summary>
public sealed class ChannelService(IChannelRepository repo, IClock clock) : IChannelService
{
    public Task<Channel?> GetAsync(Guid officeId, string kind, CancellationToken ct = default)
        => repo.GetAsync(officeId, kind, ct);

    public Task<IReadOnlyList<Channel>> ListAsync(Guid officeId, CancellationToken ct = default)
        => repo.ListAsync(officeId, ct);

    public Task<Channel> SaveEmailAsync(Guid officeId, bool enabled, string? fromAddress, string? fromName, SecretUpdate appPassword, CancellationToken ct = default)
        => repo.SaveEmailAsync(officeId, enabled, fromAddress, fromName, appPassword, clock.UtcNow, ct);

    public Task<Channel> SaveTelegramAsync(Guid officeId, bool enabled, SecretUpdate botToken, CancellationToken ct = default)
        => repo.SaveTelegramAsync(officeId, enabled, botToken, clock.UtcNow, ct);

    public Task<Channel> SaveWhatsAppAsync(Guid officeId, bool enabled, string? baseUrl, SecretUpdate token, CancellationToken ct = default)
        => repo.SaveWhatsAppAsync(officeId, enabled, baseUrl, token, clock.UtcNow, ct);

    public async Task<string> CreateLinkAsync(Guid officeId, string kind, CancellationToken ct = default)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)); // 8 hex chars
        await repo.SetLinkCodeAsync(officeId, kind, code, clock.UtcNow, ct);
        return code;
    }

    public Task<bool> DeleteLinkAsync(Guid officeId, string kind, CancellationToken ct = default)
        => repo.DeleteLinkAsync(officeId, kind, clock.UtcNow, ct);
}

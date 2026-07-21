using System.Text;
using Korkut.Application.Abstractions.Persistence;
using Korkut.Application.Security;
using Korkut.Domain.Channels;
using Korkut.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="IChannelRepository"/>. Provider secrets are
/// encrypted at rest via <see cref="ISecretEncryptor"/> and never leave the adapter; the
/// domain model only carries a <c>HasSecret</c> flag. Each kind has at most one row; writes
/// upsert it. Tenant context is set by the caller (RLS).
/// </summary>
public sealed class ChannelRepository(KorkutDbContext db, ISecretEncryptor encryptor) : IChannelRepository
{
    public async Task<Channel?> GetAsync(Guid officeId, string kind, CancellationToken ct = default)
    {
        var row = await db.Channels.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Kind == kind, ct);
        return row is null ? null : ToDomain(row);
    }

    public async Task<IReadOnlyList<Channel>> ListAsync(Guid officeId, CancellationToken ct = default)
        => (await db.Channels.AsNoTracking().Where(x => x.OfficeId == officeId).ToListAsync(ct))
            .Select(ToDomain).ToList();

    public async Task<Channel> SaveEmailAsync(
        Guid officeId, bool enabled, string? fromAddress, string? fromName, SecretUpdate appPassword, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        var row = await UpsertAsync(officeId, ChannelKind.Email, ct);
        row.Enabled = enabled;
        row.FromAddress = fromAddress;
        row.FromName = fromName;
        ApplySecret(row, appPassword);
        row.UpdatedAt = updatedAt;
        await db.SaveChangesAsync(ct);
        return ToDomain(row);
    }

    public async Task<Channel> SaveTelegramAsync(Guid officeId, bool enabled, SecretUpdate botToken, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        var row = await UpsertAsync(officeId, ChannelKind.Telegram, ct);
        row.Enabled = enabled;
        ApplySecret(row, botToken);
        row.UpdatedAt = updatedAt;
        await db.SaveChangesAsync(ct);
        return ToDomain(row);
    }

    public async Task<Channel> SaveWhatsAppAsync(Guid officeId, bool enabled, string? baseUrl, SecretUpdate token, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        var row = await UpsertAsync(officeId, ChannelKind.WhatsApp, ct);
        row.Enabled = enabled;
        row.BaseUrl = baseUrl;
        ApplySecret(row, token);
        row.UpdatedAt = updatedAt;
        await db.SaveChangesAsync(ct);
        return ToDomain(row);
    }

    public async Task SetLinkCodeAsync(Guid officeId, string kind, string code, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        var row = await UpsertAsync(officeId, kind, ct);
        row.LinkCode = code;
        row.Linked = false;
        row.LinkedId = null;
        row.UpdatedAt = updatedAt;
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteLinkAsync(Guid officeId, string kind, DateTimeOffset updatedAt, CancellationToken ct = default)
        => await db.Channels.Where(x => x.OfficeId == officeId && x.Kind == kind)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Linked, false)
                .SetProperty(x => x.LinkedId, (string?)null)
                .SetProperty(x => x.LinkCode, (string?)null)
                .SetProperty(x => x.UpdatedAt, updatedAt), ct) > 0;

    private async Task<ChannelEntity> UpsertAsync(Guid officeId, string kind, CancellationToken ct)
    {
        var row = await db.Channels.SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Kind == kind, ct);
        if (row is null)
        {
            row = new ChannelEntity { OfficeId = officeId, Kind = kind };
            db.Channels.Add(row);
        }

        return row;
    }

    private void ApplySecret(ChannelEntity row, SecretUpdate update)
    {
        if (update.Clear)
        {
            row.EncryptedSecret = null;
        }
        else if (!update.Keep && update.NewValue is not null)
        {
            row.EncryptedSecret = encryptor.Encrypt(Encoding.UTF8.GetBytes(update.NewValue));
        }
        // Keep: leave EncryptedSecret unchanged.
    }

    private static Channel ToDomain(ChannelEntity e) =>
        new(e.Kind, e.Enabled, e.FromAddress, e.FromName, e.BaseUrl, e.Linked, e.LinkedId, e.EncryptedSecret != null);
}

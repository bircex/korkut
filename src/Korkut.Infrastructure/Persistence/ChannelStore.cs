using System.Security.Cryptography;
using System.Text;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// How a secret field on a channel should change: keep the stored value, clear it, or
/// set a new plaintext value (which the store encrypts). Mirrors the UI contract where
/// the secret is <c>null</c> to keep, <c>""</c> to clear, or a value to replace.
/// </summary>
public readonly record struct SecretUpdate(bool Keep, bool Clear, string? NewValue)
{
    public static SecretUpdate From(string? raw) => raw switch
    {
        null => new SecretUpdate(true, false, null),
        "" => new SecretUpdate(false, true, null),
        _ => new SecretUpdate(false, false, raw),
    };
}

/// <summary>
/// Messaging-channel configuration (email / telegram / whatsapp) per office. Provider
/// secrets are encrypted at rest and never returned. Tenant context is set by the
/// caller (RLS). Each kind has at most one row; writes upsert it.
/// </summary>
public sealed class ChannelStore(KorkutDbContext db, ISecretEncryptor encryptor, IClock clock)
{
    public const string Email = "email";
    public const string Telegram = "telegram";
    public const string WhatsApp = "whatsapp";

    public Task<ChannelEntity?> GetAsync(Guid officeId, string kind, CancellationToken ct = default)
        => db.Channels.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Kind == kind, ct);

    public Task<List<ChannelEntity>> ListAsync(Guid officeId, CancellationToken ct = default)
        => db.Channels.AsNoTracking().Where(x => x.OfficeId == officeId).ToListAsync(ct);

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

    public async Task<ChannelEntity> SaveEmailAsync(
        Guid officeId, bool enabled, string? fromAddress, string? fromName, SecretUpdate appPassword, CancellationToken ct = default)
    {
        var row = await UpsertAsync(officeId, Email, ct);
        row.Enabled = enabled;
        row.FromAddress = fromAddress;
        row.FromName = fromName;
        ApplySecret(row, appPassword);
        row.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<ChannelEntity> SaveTelegramAsync(Guid officeId, bool enabled, SecretUpdate botToken, CancellationToken ct = default)
    {
        var row = await UpsertAsync(officeId, Telegram, ct);
        row.Enabled = enabled;
        ApplySecret(row, botToken);
        row.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return row;
    }

    public async Task<ChannelEntity> SaveWhatsAppAsync(Guid officeId, bool enabled, string? baseUrl, SecretUpdate token, CancellationToken ct = default)
    {
        var row = await UpsertAsync(officeId, WhatsApp, ct);
        row.Enabled = enabled;
        row.BaseUrl = baseUrl;
        ApplySecret(row, token);
        row.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return row;
    }

    /// <summary>Mint a fresh one-time pairing code for a kind (unlinks any prior pairing).</summary>
    public async Task<string> CreateLinkAsync(Guid officeId, string kind, CancellationToken ct = default)
    {
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)); // 8 hex chars
        var row = await UpsertAsync(officeId, kind, ct);
        row.LinkCode = code;
        row.Linked = false;
        row.LinkedId = null;
        row.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return code;
    }

    /// <summary>Remove the pairing (code + linked state) for a kind. Returns false if there was no row.</summary>
    public async Task<bool> DeleteLinkAsync(Guid officeId, string kind, CancellationToken ct = default)
    {
        var affected = await db.Channels.Where(x => x.OfficeId == officeId && x.Kind == kind)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Linked, false)
                .SetProperty(x => x.LinkedId, (string?)null)
                .SetProperty(x => x.LinkCode, (string?)null)
                .SetProperty(x => x.UpdatedAt, clock.UtcNow), ct);
        return affected > 0;
    }
}

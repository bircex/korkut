using Korkut.Application.Abstractions.Persistence;
using Korkut.Domain.Settings;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Domain.Ids;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF/Postgres adapter for <see cref="IOfficeSettingsRepository"/>. Persona and office
/// style are singletons by natural key (upsert). Memory items resolve their author id to
/// the user's email for display. Tenant context is set by the caller (RLS).
/// </summary>
public sealed class OfficeSettingsRepository(KorkutDbContext db) : IOfficeSettingsRepository
{
    // --- Persona (one row per office) ---

    public async Task<Persona?> GetPersonaAsync(Guid officeId, CancellationToken ct = default)
    {
        var p = await db.Personas.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId, ct);
        return p is null ? null : new Persona(p.Tone, p.Expertise, p.ExtraRules);
    }

    public async Task<Persona> UpsertPersonaAsync(Guid officeId, string tone, string expertise, string extraRules, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        var existing = await db.Personas.SingleOrDefaultAsync(x => x.OfficeId == officeId, ct);
        if (existing is null)
        {
            existing = new PersonaEntity { OfficeId = officeId };
            db.Personas.Add(existing);
        }

        existing.Tone = tone;
        existing.Expertise = expertise;
        existing.ExtraRules = extraRules;
        existing.UpdatedAt = updatedAt;
        await db.SaveChangesAsync(ct);
        return new Persona(existing.Tone, existing.Expertise, existing.ExtraRules);
    }

    // --- Style profiles (office + user layers) ---

    public async Task<StyleFields> GetStyleAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default)
    {
        var row = await db.StyleProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Scope == scope && x.UserId == userId, ct);
        return row is null ? StyleFields.Empty : new StyleFields(row.Letterhead, row.Signature, row.Tone, row.Boilerplate);
    }

    public async Task SaveStyleAsync(Guid officeId, string scope, Guid userId, StyleFields fields, DateTimeOffset updatedAt, CancellationToken ct = default)
    {
        var row = await db.StyleProfiles
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Scope == scope && x.UserId == userId, ct);
        if (row is null)
        {
            row = new StyleProfileEntity { Id = UuidV7.NewId(), OfficeId = officeId, Scope = scope, UserId = userId };
            db.StyleProfiles.Add(row);
        }

        row.Letterhead = fields.Letterhead;
        row.Signature = fields.Signature;
        row.Tone = fields.Tone;
        row.Boilerplate = fields.Boilerplate;
        row.UpdatedAt = updatedAt;
        await db.SaveChangesAsync(ct);
    }

    // --- Memory ---

    public async Task<IReadOnlyList<MemoryItem>> ListMemoryAsync(Guid officeId, CancellationToken ct = default)
    {
        var items = await db.MemoryItems.AsNoTracking()
            .Where(x => x.OfficeId == officeId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return await ToViewsAsync(items, ct);
    }

    public async Task<MemoryItem> AddMemoryAsync(Guid officeId, string text, string kind, Guid authorId, bool active, DateTimeOffset createdAt, CancellationToken ct = default)
    {
        var item = new MemoryItemEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Text = text,
            Kind = kind,
            Status = active ? "active" : "pending",
            AuthorId = authorId,
            CreatedAt = createdAt,
        };
        db.MemoryItems.Add(item);
        await db.SaveChangesAsync(ct);
        return (await ToViewsAsync([item], ct))[0];
    }

    public async Task<MemoryItem?> ApproveMemoryAsync(Guid officeId, Guid id, CancellationToken ct = default)
    {
        var affected = await db.MemoryItems.Where(x => x.OfficeId == officeId && x.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "active"), ct);
        if (affected == 0)
        {
            return null;
        }

        var item = await db.MemoryItems.AsNoTracking().SingleAsync(x => x.OfficeId == officeId && x.Id == id, ct);
        return (await ToViewsAsync([item], ct))[0];
    }

    public async Task<bool> DeleteMemoryAsync(Guid officeId, Guid id, CancellationToken ct = default)
        => await db.MemoryItems.Where(x => x.OfficeId == officeId && x.Id == id).ExecuteDeleteAsync(ct) > 0;

    /// <summary>Resolve author ids to emails in one batch, then map the entities to views.</summary>
    private async Task<List<MemoryItem>> ToViewsAsync(List<MemoryItemEntity> items, CancellationToken ct)
    {
        var authorIds = items.Where(i => i.AuthorId != null).Select(i => i.AuthorId!.Value).Distinct().ToList();
        var emails = authorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        return items.Select(i => new MemoryItem(
            i.Id, i.Kind, i.Text, i.Status,
            i.AuthorId != null && emails.TryGetValue(i.AuthorId.Value, out var e) ? e : null,
            i.CreatedAt)).ToList();
    }
}

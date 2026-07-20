using Korkut.Infrastructure.Persistence.Entities;
using Korkut.SharedKernel.Ids;
using Korkut.SharedKernel.Time;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>The four drafting-style fields, used at every style layer.</summary>
public readonly record struct StyleFields(string Letterhead, string Signature, string Tone, string Boilerplate)
{
    public static readonly StyleFields Empty = new(string.Empty, string.Empty, string.Empty, string.Empty);

    /// <summary>Overlay: this layer's non-empty fields win over <paramref name="baseline"/>.</summary>
    public StyleFields Over(StyleFields baseline) => new(
        string.IsNullOrWhiteSpace(Letterhead) ? baseline.Letterhead : Letterhead,
        string.IsNullOrWhiteSpace(Signature) ? baseline.Signature : Signature,
        string.IsNullOrWhiteSpace(Tone) ? baseline.Tone : Tone,
        string.IsNullOrWhiteSpace(Boilerplate) ? baseline.Boilerplate : Boilerplate);
}

/// <summary>
/// Office settings: the AI persona (office-level singleton), drafting style profiles
/// (office + per-user layers), and the office memory list. Tenant context is set by the
/// caller (RLS). Persona/office-style upserts are idempotent by their natural key.
/// </summary>
public sealed class OfficeSettingsStore(KorkutDbContext db, IClock clock)
{
    // --- Persona (one row per office) ---

    public Task<PersonaEntity?> GetPersonaAsync(Guid officeId, CancellationToken ct = default)
        => db.Personas.AsNoTracking().SingleOrDefaultAsync(x => x.OfficeId == officeId, ct);

    public async Task<PersonaEntity> UpsertPersonaAsync(Guid officeId, string tone, string expertise, string extraRules, CancellationToken ct = default)
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
        existing.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return existing;
    }

    // --- Style profiles (office + user layers) ---

    public async Task<StyleFields> GetStyleAsync(Guid officeId, string scope, Guid userId, CancellationToken ct = default)
    {
        var row = await db.StyleProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.OfficeId == officeId && x.Scope == scope && x.UserId == userId, ct);
        return row is null ? StyleFields.Empty : new StyleFields(row.Letterhead, row.Signature, row.Tone, row.Boilerplate);
    }

    public async Task<StyleFields> SaveStyleAsync(Guid officeId, string scope, Guid userId, StyleFields fields, CancellationToken ct = default)
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
        row.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return fields;
    }

    // --- Memory ---

    /// <summary>A memory item with its author resolved to a display value (the user's email).</summary>
    public sealed record MemoryView(Guid Id, string Kind, string Text, string Status, string? Author, DateTimeOffset CreatedAt);

    /// <summary>Resolve author ids to emails in one batch, then map the entities to views.</summary>
    private async Task<List<MemoryView>> ToViewsAsync(List<MemoryItemEntity> items, CancellationToken ct)
    {
        var authorIds = items.Where(i => i.AuthorId != null).Select(i => i.AuthorId!.Value).Distinct().ToList();
        var emails = authorIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        return items.Select(i => new MemoryView(
            i.Id, i.Kind, i.Text, i.Status,
            i.AuthorId != null && emails.TryGetValue(i.AuthorId.Value, out var e) ? e : null,
            i.CreatedAt)).ToList();
    }

    public async Task<List<MemoryView>> ListMemoryAsync(Guid officeId, CancellationToken ct = default)
    {
        var items = await db.MemoryItems.AsNoTracking()
            .Where(x => x.OfficeId == officeId).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return await ToViewsAsync(items, ct);
    }

    public async Task<MemoryView> AddMemoryAsync(Guid officeId, string text, string kind, Guid authorId, bool active, CancellationToken ct = default)
    {
        var item = new MemoryItemEntity
        {
            Id = UuidV7.NewId(),
            OfficeId = officeId,
            Text = text,
            Kind = kind,
            Status = active ? "active" : "pending",
            AuthorId = authorId,
            CreatedAt = clock.UtcNow,
        };
        db.MemoryItems.Add(item);
        await db.SaveChangesAsync(ct);
        return (await ToViewsAsync([item], ct))[0];
    }

    public async Task<MemoryView?> ApproveMemoryAsync(Guid officeId, Guid id, CancellationToken ct = default)
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
}

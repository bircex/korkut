using Korkut.Clients;
using Korkut.Clients.Entities;
using Korkut.Hearings;
using Korkut.Hearings.Entities;
using Korkut.Matters;
using Korkut.Matters.Entities;
using Korkut.Organization.Entities;
using Korkut.Reminders.Entities;
using Korkut.Telegram.Entities;
using Microsoft.EntityFrameworkCore;

namespace Korkut.Infrastructure.Persistence;

/// <summary>
/// Cross-module FK relationships that are REAL DB constraints but cannot live in a module's own
/// config (a module never references another module's entity type). Configured here, at the
/// composition level, which can see every module's entities. Intra-module FKs (e.g. Party→Matter)
/// stay in their module config.
/// </summary>
internal static class CrossModuleRelationships
{
    public static void Apply(ModelBuilder modelBuilder)
    {
        // Matter → Client (tenant-aware composite FK, optional).
        modelBuilder.Entity<Matter>()
            .HasOne<Client>()
            .WithMany()
            .HasForeignKey(x => new { x.OfficeId, x.ClientId })
            .HasPrincipalKey(c => new { c.OfficeId, c.Id })
            .OnDelete(DeleteBehavior.SetNull);

        // Hearing → Matter (tenant-aware composite FK).
        modelBuilder.Entity<Hearing>()
            .HasOne<Matter>()
            .WithMany()
            .HasForeignKey(x => new { x.OfficeId, x.MatterId })
            .HasPrincipalKey(m => new { m.OfficeId, m.Id })
            .OnDelete(DeleteBehavior.Cascade);

        // Leaving the office takes a member off every matter's team.
        modelBuilder.Entity<Korkut.Matters.Entities.MatterMember>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // A member's Telegram link goes with the member; the bot goes with the office.
        modelBuilder.Entity<TelegramLink>()
            .HasOne<User>()
            .WithOne()
            .HasForeignKey<TelegramLink>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        // A group bound to a matter goes with the matter. Who is in a group is known before it is
        // bound (people already there must count too), so those rows stand on their own.
        modelBuilder.Entity<TelegramGroup>()
            .HasOne<Matter>()
            .WithMany()
            .HasForeignKey(x => new { x.OfficeId, x.MatterId })
            .HasPrincipalKey(m => new { m.OfficeId, m.Id })
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TelegramBot>()
            .HasOne<Office>()
            .WithOne()
            .HasForeignKey<TelegramBot>(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<SentReminder>()
            .HasOne<Office>()
            .WithMany()
            .HasForeignKey(x => x.OfficeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

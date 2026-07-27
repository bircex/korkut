using Korkut.Clients;
using Korkut.Clients.Entities;
using Korkut.Hearings;
using Korkut.Hearings.Entities;
using Korkut.Matters;
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
    }
}

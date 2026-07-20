using System.Net.Http.Headers;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Infrastructure.Security;
using Korkut.Application.Security;
using Korkut.SharedKernel.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Korkut.Api.Tests.Integration;

/// <summary>
/// Boots the real API against a real PostgreSQL (Testcontainers), so DB-backed
/// endpoints are exercised end-to-end WITH row-level security. The host connects as
/// the least-privileged <c>korkut_app</c> role (never a superuser), exactly like
/// production, so RLS is actually in force during the tests. Provides helpers to seed
/// an office/user and mint an access token for it.
/// </summary>
public sealed class KorkutApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private string _appConnectionString = string.Empty;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        // Migrate as the owner, then provision a login for the least-privileged app role.
        await using (var db = OwnerContext())
        {
            await db.Database.MigrateAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER ROLE korkut_app WITH LOGIN PASSWORD 'approle_test'");
        }

        _appConnectionString = new NpgsqlConnectionStringBuilder(_pg.GetConnectionString())
        {
            Username = "korkut_app",
            Password = "approle_test",
        }.ConnectionString;

        Environment.SetEnvironmentVariable("KORKUT_DB_URL", _appConnectionString);
        // Trigger host construction now so it picks up the connection string.
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await _pg.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("KORKUT_DB_URL", _appConnectionString);
        builder.UseEnvironment("Testing");
    }

    /// <summary>Owner/superuser context for seeding (RLS bypassed) — tests only.</summary>
    public KorkutDbContext OwnerContext()
    {
        var options = new DbContextOptionsBuilder<KorkutDbContext>()
            .UseNpgsql(_pg.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, PersistenceSetup.Schema))
            .Options;
        return new KorkutDbContext(options);
    }

    /// <summary>Seed an office + a user and return their ids.</summary>
    public async Task<(Guid OfficeId, Guid UserId)> SeedOfficeAsync(string role = Roles.OfficeOwner)
    {
        var officeId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        await using var db = OwnerContext();
        db.Offices.Add(new OfficeEntity { Id = officeId, Name = "Test Bürosu", CreatedAt = DateTimeOffset.UtcNow });
        db.Users.Add(new UserEntity
        {
            Id = userId,
            OfficeId = officeId,
            Email = $"user-{userId:N}@example.com",
            Role = role,
            SecurityStamp = Guid.CreateVersion7(),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return (officeId, userId);
    }

    /// <summary>Seed an office + a user with a known password (for local-login tests).</summary>
    public async Task<(Guid OfficeId, Guid UserId, string Email)> SeedUserWithPasswordAsync(
        string password, string role = Roles.OfficeOwner, bool twoFactor = false)
    {
        var officeId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var email = $"user-{userId:N}@example.com";
        var hash = new Pbkdf2PasswordHasher().Hash(password);
        await using var db = OwnerContext();
        db.Offices.Add(new OfficeEntity { Id = officeId, Name = "Test Bürosu", CreatedAt = DateTimeOffset.UtcNow });
        db.Users.Add(new UserEntity
        {
            Id = userId,
            OfficeId = officeId,
            Email = email,
            PasswordHash = hash,
            Role = role,
            TwoFactorEnabled = twoFactor,
            SecurityStamp = Guid.CreateVersion7(),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return (officeId, userId, email);
    }

    /// <summary>A cookie-aware client (no auth) — for exercising the login → cookie → /me flow.</summary>
    public HttpClient CookieClient() => CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });

    /// <summary>An HttpClient carrying a valid access token for the given office/user.</summary>
    public HttpClient ClientFor(Guid officeId, Guid userId, string role = Roles.OfficeOwner)
    {
        var token = new AccessTokenService(Environment.GetEnvironmentVariable("KORKUT_SECRET")!, new Clock())
            .Mint(new AccessClaims(userId, officeId, role, "s1"));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}

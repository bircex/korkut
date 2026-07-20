using System.Net.Http.Headers;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Infrastructure.Security;
using Korkut.Application.Security;
using Korkut.SharedKernel.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.Minio;
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
    private readonly MinioContainer _minio = new MinioBuilder("minio/minio:RELEASE.2025-04-08T15-41-24Z").Build();
    private string _appConnectionString = string.Empty;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_pg.StartAsync(), _minio.StartAsync());

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
        var minioEndpoint = new Uri(_minio.GetConnectionString());
        Environment.SetEnvironmentVariable("KORKUT_MINIO_ENDPOINT", $"{minioEndpoint.Host}:{minioEndpoint.Port}");
        Environment.SetEnvironmentVariable("KORKUT_MINIO_ACCESS_KEY", _minio.GetAccessKey());
        Environment.SetEnvironmentVariable("KORKUT_MINIO_SECRET_KEY", _minio.GetSecretKey());
        // Trigger host construction now so it picks up the connection strings.
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await _pg.DisposeAsync();
        await _minio.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("KORKUT_DB_URL", _appConnectionString);
        // This one shared host serves the whole integration suite, so the per-IP
        // sensitive-endpoint limiter would throttle across tests. Raise it here only
        // (the dedicated rate-limit test uses its own strict host).
        builder.UseSetting("KORKUT_SENSITIVE_RATE_LIMIT", "100000");
        // Configure Google OIDC on THIS host only (dummy creds) so start/callback work;
        // the real HTTP exchanger + JWKS validator are replaced with deterministic fakes.
        builder.UseSetting("GOOGLE_CLIENT_ID", "test-client-id");
        builder.UseSetting("GOOGLE_CLIENT_SECRET", "test-client-secret");
        builder.UseSetting("GOOGLE_REDIRECT_URI", "https://app.example.com/api/auth/google/callback");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOidcTokenExchanger>();
            services.AddSingleton<IOidcTokenExchanger, FakeTokenExchanger>();
            services.RemoveAll<IGoogleIdTokenValidator>();
            services.AddSingleton<IGoogleIdTokenValidator, FakeIdTokenValidator>();
        });
        builder.UseEnvironment("Testing");
    }

    /// <summary>Passes the auth code straight through as the "id token".</summary>
    private sealed class FakeTokenExchanger : IOidcTokenExchanger
    {
        public Task<string> ExchangeForIdTokenAsync(string code, string codeVerifier, CancellationToken ct = default)
            => Task.FromResult(code);
    }

    /// <summary>Decodes a test id token of the form "sub|email|emailVerified|nonce".</summary>
    private sealed class FakeIdTokenValidator : IGoogleIdTokenValidator
    {
        public Task<GoogleIdentity> ValidateAsync(string idToken, CancellationToken ct = default)
        {
            var parts = idToken.Split('|');
            return Task.FromResult(new GoogleIdentity(parts[0], parts[1], bool.Parse(parts[2]), parts[3]));
        }
    }

    /// <summary>Build a test id token the FakeIdTokenValidator understands.</summary>
    public static string FakeIdToken(string sub, string email, string nonce, bool emailVerified = true)
        => $"{sub}|{email}|{emailVerified}|{nonce}";

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
    public HttpClient CookieClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    /// <summary>A cookie-aware client that does NOT follow redirects (for OAuth start/callback).</summary>
    public HttpClient NoRedirectCookieClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    /// <summary>Seed an active password-reset token for a user; returns the raw token to submit.</summary>
    public async Task<string> SeedResetTokenAsync(Guid officeId, Guid userId)
    {
        var raw = "reset-" + Guid.CreateVersion7().ToString("N");
        var hash = new Sha256TokenHasher().Hash(raw);
        await using var db = OwnerContext();
        db.PasswordResetTokens.Add(new PasswordResetTokenEntity
        {
            Id = Guid.CreateVersion7(),
            OfficeId = officeId,
            UserId = userId,
            TokenHash = hash,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        return raw;
    }

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

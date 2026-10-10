using Korkut.Organization;
using Korkut.Organization.Entities;
using System.Net.Http.Headers;
using Korkut.Infrastructure.Persistence;
using Korkut.Security;
using Korkut.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Korkut.TestSupport;

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
    private readonly string _objectsRoot = Path.Combine(Path.GetTempPath(), "korkut-objects-" + Guid.NewGuid().ToString("N"));
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
        // Host secrets, as env vars so BOTH the API host and this factory's own token-minting
        // (ClientFor) see them — no per-test-assembly TestEnv needed. Fixed non-production values.
        Environment.SetEnvironmentVariable("KORKUT_SECRET", "korkut-integration-test-signing-secret-32b+");
        Environment.SetEnvironmentVariable("KORKUT_TOTP_KEY", Convert.ToBase64String(new byte[32]));
        Environment.SetEnvironmentVariable("KORKUT_STORAGE", "filesystem");
        Environment.SetEnvironmentVariable("KORKUT_STORAGE_PATH", _objectsRoot);
        // Trigger host construction now so it picks up the connection strings.
        _ = Server;
    }

    public new async Task DisposeAsync()
    {
        await _pg.DisposeAsync();
        if (Directory.Exists(_objectsRoot))
        {
            Directory.Delete(_objectsRoot, recursive: true);
        }

        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("KORKUT_DB_URL", _appConnectionString);
        // Host secrets the API needs to boot (fixed, non-production test values). Set here so
        // every endpoint-test assembly gets them from the shared factory — no per-project env setup.
        builder.UseSetting("KORKUT_SECRET", "korkut-integration-test-signing-secret-32b+");
        builder.UseSetting("KORKUT_TOTP_KEY", Convert.ToBase64String(new byte[32]));
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
        await using var db = OwnerContext();
        db.Offices.Add(new Office(officeId, "Test Bürosu", DateTimeOffset.UtcNow));
        var user = new User(officeId, $"user-{Guid.CreateVersion7():N}@example.com", role, null, null, DateTimeOffset.UtcNow);
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (officeId, user.Id);
    }

    /// <summary>Seed an office + a user with a known password (for local-login tests).</summary>
    public async Task<(Guid OfficeId, Guid UserId, string Email)> SeedUserWithPasswordAsync(
        string password, string role = Roles.OfficeOwner, bool twoFactor = false)
    {
        var officeId = Guid.CreateVersion7();
        var email = $"user-{Guid.CreateVersion7():N}@example.com";
        await using var db = OwnerContext();
        db.Offices.Add(new Office(officeId, "Test Bürosu", DateTimeOffset.UtcNow));
        var user = new User(officeId, email, role, null, null, DateTimeOffset.UtcNow);
        user.SetPassword(new Pbkdf2PasswordHasher().Hash(password));
        if (twoFactor)
        {
            user.EnableTwoFactor();
        }

        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (officeId, user.Id, email);
    }

    /// <summary>
    /// Seed an office + a Google-linked user WITHOUT a local password (SSO-only). Used to prove a
    /// reset request now issues a token for such accounts too.
    /// </summary>
    public async Task<(Guid OfficeId, Guid UserId, string Email)> SeedGoogleUserAsync(string role = Roles.OfficeOwner)
    {
        var officeId = Guid.CreateVersion7();
        var email = $"user-{Guid.CreateVersion7():N}@example.com";
        await using var db = OwnerContext();
        db.Offices.Add(new Office(officeId, "Test Bürosu", DateTimeOffset.UtcNow));
        var user = new User(officeId, email, role, null, null, DateTimeOffset.UtcNow);
        user.LinkGoogle($"google-sub-{Guid.CreateVersion7():N}");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (officeId, user.Id, email);
    }

    /// <summary>Count active (unused, unexpired) reset tokens for a user — asserts a request issued one.</summary>
    public async Task<int> CountActiveResetTokensAsync(Guid userId)
    {
        await using var db = OwnerContext();
        return await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null && t.ExpiresAt > DateTimeOffset.UtcNow)
            .CountAsync();
    }

    /// <summary>A cookie-aware client (no auth) — for exercising the login → cookie → /me flow.</summary>
    public HttpClient CookieClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });

    /// <summary>A cookie-aware client that does NOT follow redirects (for OAuth start/callback).</summary>
    public HttpClient NoRedirectCookieClient() => CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });

    /// <summary>An HttpClient carrying a valid PLATFORM-ADMIN token (separate audience).</summary>
    public HttpClient AdminClient()
    {
        var token = new AdminAccessTokens(Environment.GetEnvironmentVariable("KORKUT_SECRET")!, new Clock())
            .Mint(new AccessClaims(Guid.CreateVersion7(), Guid.Empty, "admin", "s1"));
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Seed an active password-reset token for a user; returns the raw token to submit.</summary>
    public async Task<string> SeedResetTokenAsync(Guid officeId, Guid userId)
    {
        var raw = "reset-" + Guid.CreateVersion7().ToString("N");
        var hash = new Sha256TokenHasher().Hash(raw);
        await using var db = OwnerContext();
        db.PasswordResetTokens.Add(new PasswordResetToken(
            officeId, userId, hash, DateTimeOffset.UtcNow.AddHours(1), DateTimeOffset.UtcNow));
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

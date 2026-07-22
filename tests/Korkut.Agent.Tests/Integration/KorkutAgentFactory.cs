using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using Korkut.Agent.Application;
using Korkut.Application.Ai;
using Korkut.Application.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Agent;
using Korkut.Infrastructure.Persistence.Entities;
using Korkut.Infrastructure.Security;
using Korkut.SharedKernel.Time;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Korkut.Agent.Tests.Integration;

/// <summary>
/// Boots the real Agent host against a real PostgreSQL (Testcontainers) with BOTH schemas
/// migrated — the operational <c>korkut</c> (office settings the assistant reads) and the
/// Agent <c>korkut_agent</c> (conversations + drafted documents). The host connects as the
/// least-privileged <c>korkut_app</c> role, so row-level security is actually in force. A
/// deterministic fake <see cref="IChatModel"/> stands in for the provider, so the full chat/
/// drafting path runs end-to-end without a real model key.
/// </summary>
public sealed class KorkutAgentFactory : WebApplicationFactory<Program>, IAsyncLifetime
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

        // Migrate the operational schema, then the Agent schema (which GRANTs to korkut_app,
        // so the app role must already exist), then provision a login for that app role.
        await using (var db = OwnerContext())
        {
            await db.Database.MigrateAsync();
        }

        await using (var agent = AgentOwnerContext())
        {
            await agent.Database.MigrateAsync();
        }

        await using (var db = OwnerContext())
        {
            await db.Database.ExecuteSqlRawAsync("ALTER ROLE korkut_app WITH LOGIN PASSWORD 'approle_test'");
        }

        _appConnectionString = new NpgsqlConnectionStringBuilder(_pg.GetConnectionString())
        {
            Username = "korkut_app",
            Password = "approle_test",
        }.ConnectionString;

        // Both hosts' DbContexts (operational + agent) share the same database + app role,
        // separated only by schema, so the one app-role connection string serves both.
        Environment.SetEnvironmentVariable("KORKUT_DB_URL", _appConnectionString);
        Environment.SetEnvironmentVariable("KORKUT_AGENT_DB_URL", _appConnectionString);
        _ = Server; // Trigger host construction now that the connection strings are set.
    }

    public new async Task DisposeAsync()
    {
        await _pg.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("KORKUT_DB_URL", _appConnectionString);
        builder.UseSetting("KORKUT_AGENT_DB_URL", _appConnectionString);
        builder.ConfigureTestServices(services =>
        {
            // Deterministic model so chat/drafting is exercised end-to-end without a provider key.
            services.RemoveAll<IChatModel>();
            services.AddScoped<IChatModel, FakeChatModel>();
        });
        builder.UseEnvironment("Testing");
    }

    /// <summary>A deterministic assistant: echoes the last user turn (proves the prompt was built).</summary>
    private sealed class FakeChatModel : IChatModel
    {
        // A non-empty usage so the llm_calls telemetry ledger records a meaningful row.
        internal static readonly ChatUsage FakeUsage = new("fake", "fake-model", 12, 34);

        private static string Reply(ChatRequest request)
            => $"[assistant] {request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty}";

        public Task<ChatReply> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatReply(Reply(request), FakeUsage));

        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
            ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var word in Reply(request).Split(' '))
            {
                await Task.Yield();
                yield return ChatStreamEvent.Text(word + " ");
            }

            yield return ChatStreamEvent.Final(FakeUsage);
        }
    }

    /// <summary>Owner/superuser context for the operational schema (RLS bypassed) — tests only.</summary>
    public KorkutDbContext OwnerContext()
    {
        var options = new DbContextOptionsBuilder<KorkutDbContext>()
            .UseNpgsql(_pg.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, PersistenceSetup.Schema))
            .Options;
        return new KorkutDbContext(options);
    }

    /// <summary>Owner/superuser context for the Agent schema (RLS bypassed) — tests only.</summary>
    public AgentDbContext AgentOwnerContext()
    {
        var options = new DbContextOptionsBuilder<AgentDbContext>()
            .UseNpgsql(_pg.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, PersistenceSetup.AgentSchema))
            .Options;
        return new AgentDbContext(options);
    }

    /// <summary>Seed an office + a user in the operational DB and return their ids.</summary>
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

    /// <summary>
    /// Seed an office persona + one active memory item (operational DB), so a chat turn's
    /// system prompt is composed from real settings. Used to exercise the cross-DB read path.
    /// </summary>
    public async Task SeedPersonaAndMemoryAsync(Guid officeId, Guid authorId)
    {
        await using var db = OwnerContext();
        db.Personas.Add(new PersonaEntity
        {
            OfficeId = officeId, Tone = "resmi", Expertise = "kira hukuku", ExtraRules = string.Empty, UpdatedAt = DateTimeOffset.UtcNow,
        });
        db.MemoryItems.Add(new MemoryItemEntity
        {
            Id = Guid.CreateVersion7(), OfficeId = officeId, Kind = "preference", Text = "Müvekkil hızlı yanıt ister",
            Status = "active", AuthorId = authorId, CreatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
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

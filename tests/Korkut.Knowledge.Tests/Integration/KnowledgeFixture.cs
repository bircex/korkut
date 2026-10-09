using Korkut.Ai;
using Korkut.Core;
using Korkut.Infrastructure.Persistence;
using Korkut.Knowledge.Persistence;
using Korkut.Organization;
using Korkut.Organization.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Korkut.Knowledge.Tests.Integration;

/// <summary>
/// The Knowledge module as the app host composes it (<c>AddKorkutKnowledge</c> + the shared AI
/// runtime), against a real pgvector-enabled PostgreSQL (Testcontainers). The operational schema is
/// migrated first (so the cluster-wide <c>korkut_app</c> role exists), then the Knowledge schema
/// (which grants to it), and the services connect as the least-privileged <c>korkut_app</c> role, so
/// row-level security is really enforced. A deterministic fake embedder stands in for a real model,
/// so the full chunk -> embed -> store -> search path runs end-to-end without an embedding backend.
/// </summary>
public sealed class KnowledgeFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        await using (var ops = OwnerContext())
        {
            await ops.Database.MigrateAsync();
        }

        await using (var knowledge = KnowledgeOwnerContext())
        {
            await knowledge.Database.MigrateAsync();
        }

        await using (var ops = OwnerContext())
        {
            await ops.Database.ExecuteSqlRawAsync("ALTER ROLE korkut_app WITH LOGIN PASSWORD 'approle_test'");
        }

        var appConnection = new NpgsqlConnectionStringBuilder(_pg.GetConnectionString())
        {
            Username = "korkut_app",
            Password = "approle_test",
        }.ConnectionString;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKorkutKnowledge(appConnection);
        services.AddKorkutAi();
        services.RemoveAll<IEmbedder>();
        services.AddSingleton<IEmbedder, FakeEmbedder>();
        services.AddScoped<TestCurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestCurrentUser>());
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _pg.DisposeAsync();
    }

    /// <summary>A DI scope acting as a signed-in member of <paramref name="officeId"/>, like one request.</summary>
    public AsyncServiceScope ScopeFor(Guid officeId)
    {
        var scope = _services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TestCurrentUser>().OfficeId = officeId;
        return scope;
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid OfficeId { get; set; }

        public Guid UserId { get; } = Guid.CreateVersion7();

        public IReadOnlySet<string> Permissions { get; } = new HashSet<string>();
    }

    /// <summary>
    /// A deterministic bag-of-words embedder: each token bumps a hashed dimension, then the vector
    /// is L2-normalized. Texts that share words land close in cosine space, so search returns the
    /// genuinely-relevant chunk — no external model, fully reproducible.
    /// </summary>
    private sealed class FakeEmbedder : IEmbedder
    {
        private const int Dim = 64;

        public Task<EmbeddingResult> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        {
            var vectors = texts.Select(Embed).ToList();
            return Task.FromResult(new EmbeddingResult(vectors, "fake", $"fake:{Dim}", Dim, 0));
        }

        private static float[] Embed(string text)
        {
            var v = new float[Dim];
            foreach (var token in Tokenize(text))
            {
                v[(int)(Hash(token) % Dim)] += 1f;
            }

            var norm = MathF.Sqrt(v.Sum(x => x * x));
            if (norm > 0)
            {
                for (var i = 0; i < Dim; i++)
                {
                    v[i] /= norm;
                }
            }

            return v;
        }

        private static IEnumerable<string> Tokenize(string text)
        {
            var current = new System.Text.StringBuilder();
            foreach (var c in text.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c))
                {
                    current.Append(c);
                }
                else if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }
            }

            if (current.Length > 0)
            {
                yield return current.ToString();
            }
        }

        private static uint Hash(string token)
        {
            uint hash = 2166136261;
            foreach (var c in token)
            {
                hash = (hash ^ c) * 16777619;
            }

            return hash;
        }
    }

    /// <summary>Owner/superuser context for the operational schema (migrations + role) — tests only.</summary>
    public KorkutDbContext OwnerContext()
    {
        var options = new DbContextOptionsBuilder<KorkutDbContext>()
            .UseNpgsql(_pg.GetConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, PersistenceSetup.Schema))
            .Options;
        return new KorkutDbContext(options);
    }

    /// <summary>Owner/superuser context for the Knowledge schema (RLS bypassed) — tests only.</summary>
    public KnowledgeDbContext KnowledgeOwnerContext()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(_pg.GetConnectionString(), npgsql =>
            {
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, KnowledgeSetup.KnowledgeSchema);
                npgsql.UseVector();
            })
            .Options;
        return new KnowledgeDbContext(options);
    }

    /// <summary>Seed an office + a user in the operational DB and return their ids.</summary>
    public async Task<(Guid OfficeId, Guid UserId)> SeedOfficeAsync(string role = Roles.OfficeOwner)
    {
        var officeId = Guid.CreateVersion7();
        await using var db = OwnerContext();
        db.Offices.Add(new Office(officeId, "Test Bürosu", DateTimeOffset.UtcNow));
        var user = new User(officeId, $"user-{Guid.CreateVersion7():N}@example.com", role, null, null, DateTimeOffset.UtcNow);
        var userId = user.Id;
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return (officeId, userId);
    }

    /// <summary>Seed an office in the operational DB and return its id.</summary>
    public async Task<Guid> SeedOfficeAsync()
    {
        var officeId = Guid.CreateVersion7();
        await using var db = OwnerContext();
        db.Offices.Add(new Office(officeId, "Test Bürosu", DateTimeOffset.UtcNow));
        db.Users.Add(new User(officeId, $"user-{Guid.CreateVersion7():N}@example.com", Roles.OfficeOwner, null, null, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return officeId;
    }
}

using Korkut.Organization;
using System.Net.Http.Headers;
using Korkut.Core;
using Korkut.Infrastructure.Security;
using Korkut.Infrastructure.Persistence;
using Korkut.Infrastructure.Persistence.Knowledge;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Korkut.Knowledge.Tests.Integration;

/// <summary>
/// Boots the real Knowledge host against a real pgvector-enabled PostgreSQL (Testcontainers),
/// with the operational schema migrated first (so the cluster-wide <c>korkut_app</c> role exists)
/// and then the Knowledge schema (which grants to it). The host connects as the least-privileged
/// <c>korkut_app</c> role, so row-level security is really enforced. A deterministic fake embedder
/// stands in for a real model, so the full chunk → embed → store → search path runs end-to-end
/// without an embedding backend.
/// </summary>
public sealed class KorkutKnowledgeFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder("pgvector/pgvector:pg16").Build();
    private string _appConnectionString = string.Empty;

    private sealed class Clock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();

        // Operational schema first — its TenancyRls migration creates the cluster-wide korkut_app
        // role that the Knowledge migration grants to. Then the Knowledge schema (pgvector + RLS).
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

        _appConnectionString = new NpgsqlConnectionStringBuilder(_pg.GetConnectionString())
        {
            Username = "korkut_app",
            Password = "approle_test",
        }.ConnectionString;

        Environment.SetEnvironmentVariable("KORKUT_KNOWLEDGE_DB_URL", _appConnectionString);
        _ = Server; // Trigger host construction now that the connection string is set.
    }

    public new async Task DisposeAsync()
    {
        await _pg.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("KORKUT_KNOWLEDGE_DB_URL", _appConnectionString);
        builder.ConfigureTestServices(services =>
        {
            // Deterministic embedder so ingest + search run end-to-end without an embedding backend.
            services.RemoveAll<IEmbedder>();
            services.AddSingleton<IEmbedder, FakeEmbedder>();
        });
        builder.UseEnvironment("Testing");
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
                npgsql.MigrationsHistoryTable(PersistenceSetup.MigrationsHistoryTable, PersistenceSetup.KnowledgeSchema);
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

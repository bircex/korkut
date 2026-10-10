namespace Korkut.Knowledge.Tests.Integration;

/// <summary>
/// Shares ONE <see cref="KorkutKnowledgeFactory"/> (a single pgvector Postgres container) across
/// all Knowledge-host endpoint test classes. Tests run sequentially; each seeds its own office
/// (unique ids), so they never collide.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationSuite : ICollectionFixture<KnowledgeFixture>
{
    public const string Name = "integration";
}

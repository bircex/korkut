namespace Korkut.Agent.Tests.Integration;

/// <summary>
/// Shares ONE <see cref="KorkutAgentFactory"/> (a single Postgres container) across all
/// Agent-host endpoint test classes, so the suite doesn't spin up a container per class.
/// Tests in the collection run sequentially; each seeds its own office/user (unique ids),
/// so they never collide.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationSuite : ICollectionFixture<KorkutAgentFactory>
{
    public const string Name = "integration";
}

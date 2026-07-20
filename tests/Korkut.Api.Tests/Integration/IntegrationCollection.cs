namespace Korkut.Api.Tests.Integration;

/// <summary>
/// Shares ONE <see cref="KorkutApiFactory"/> (a single Postgres + MinIO container set)
/// across all DB-backed endpoint test classes, so the suite doesn't spin up a
/// container set per class and overwhelm the runner. Tests in the collection run
/// sequentially; each seeds its own office/user (unique ids), so they never collide.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationSuite : ICollectionFixture<KorkutApiFactory>
{
    public const string Name = "integration";
}

using Korkut.TestSupport;

namespace Korkut.Tasks.Integration.Tests;

/// <summary>Shares one KorkutApiFactory (single Postgres + MinIO container set + host) across the module's endpoint tests.</summary>
[CollectionDefinition(IntegrationSuite.Name)]
public sealed class IntegrationSuite : ICollectionFixture<KorkutApiFactory>
{
    public const string Name = "integration";
}

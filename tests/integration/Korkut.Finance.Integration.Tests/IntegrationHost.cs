using Korkut.TestSupport;

namespace Korkut.Finance.Integration.Tests;

/// <summary>Shares one KorkutApiFactory (a single Postgres + MinIO container set + host) across the module's endpoint tests.</summary>
[CollectionDefinition(IntegrationSuite.Name)]
public sealed class IntegrationSuite : ICollectionFixture<KorkutApiFactory>
{
    public const string Name = "integration";
}

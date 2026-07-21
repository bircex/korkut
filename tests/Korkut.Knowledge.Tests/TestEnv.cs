using System.Runtime.CompilerServices;

namespace Korkut.Knowledge.Tests;

/// <summary>
/// Sets the environment the Knowledge host needs to construct at startup during integration
/// tests: the JWT signing secret (same value the other test hosts use) and a placeholder
/// Knowledge DB URL so the host boots. DB-backed tests override it with a real Testcontainers
/// (pgvector) Postgres connection. Runs once before any test in this assembly.
/// </summary>
internal static class TestEnv
{
    [ModuleInitializer]
    public static void Init()
    {
        Environment.SetEnvironmentVariable("KORKUT_SECRET", "korkut-integration-test-signing-secret-32b+");
        const string placeholder = "Host=localhost;Port=1;Database=korkut;Username=placeholder;Password=placeholder";
        Environment.SetEnvironmentVariable("KORKUT_KNOWLEDGE_DB_URL", Environment.GetEnvironmentVariable("KORKUT_KNOWLEDGE_DB_URL") ?? placeholder);
    }
}

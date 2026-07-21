using System.Runtime.CompilerServices;

namespace Korkut.Agent.Tests;

/// <summary>
/// Sets the environment the Agent host needs to construct at startup during integration
/// tests: the JWT signing secret (same value the API test host uses, so a token minted for
/// one authenticates on the other) and syntactically-valid placeholder DB URLs so the host
/// boots. DB-backed tests override both URLs with a real Testcontainers Postgres connection.
/// Runs once before any test in this assembly.
/// </summary>
internal static class TestEnv
{
    [ModuleInitializer]
    public static void Init()
    {
        Environment.SetEnvironmentVariable("KORKUT_SECRET", "korkut-integration-test-signing-secret-32b+");
        const string placeholder = "Host=localhost;Port=1;Database=korkut;Username=placeholder;Password=placeholder";
        Environment.SetEnvironmentVariable("KORKUT_DB_URL", Environment.GetEnvironmentVariable("KORKUT_DB_URL") ?? placeholder);
        Environment.SetEnvironmentVariable("KORKUT_AGENT_DB_URL", Environment.GetEnvironmentVariable("KORKUT_AGENT_DB_URL") ?? placeholder);
    }
}

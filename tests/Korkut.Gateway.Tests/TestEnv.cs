using System.Runtime.CompilerServices;

namespace Korkut.Gateway.Tests;

/// <summary>
/// Sets the JWT signing secret the gateway needs to validate access tokens during integration
/// tests (the same value the other test hosts use). The upstream URLs are provided by the factory
/// once the stub servers are bound. Runs once before any test in this assembly.
/// </summary>
internal static class TestEnv
{
    [ModuleInitializer]
    public static void Init()
    {
        Environment.SetEnvironmentVariable("KORKUT_SECRET", "korkut-integration-test-signing-secret-32b+");
        Environment.SetEnvironmentVariable("KORKUT_API_URL", Environment.GetEnvironmentVariable("KORKUT_API_URL") ?? "http://127.0.0.1:9");
        Environment.SetEnvironmentVariable("KORKUT_AGENT_URL", Environment.GetEnvironmentVariable("KORKUT_AGENT_URL") ?? "http://127.0.0.1:9");
    }
}

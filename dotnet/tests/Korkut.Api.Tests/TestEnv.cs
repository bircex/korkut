using System.Runtime.CompilerServices;

namespace Korkut.Api.Tests;

/// <summary>
/// Sets the signing secret the API host needs to construct the access-token service
/// during integration tests. A fixed, non-production test value — never a real
/// secret. Runs once before any test in this assembly.
/// </summary>
internal static class TestEnv
{
    [ModuleInitializer]
    public static void Init()
    {
        Environment.SetEnvironmentVariable("KORKUT_SECRET", "korkut-integration-test-signing-secret-32b+");
    }
}

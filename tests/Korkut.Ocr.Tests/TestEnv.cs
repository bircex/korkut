using System.Runtime.CompilerServices;

namespace Korkut.Ocr.Tests;

/// <summary>
/// Sets the JWT signing secret the OCR host needs to validate access tokens during integration
/// tests (the same value the other test hosts use). Runs once before any test in this assembly.
/// </summary>
internal static class TestEnv
{
    [ModuleInitializer]
    public static void Init()
    {
        Environment.SetEnvironmentVariable("KORKUT_SECRET", "korkut-integration-test-signing-secret-32b+");
    }
}

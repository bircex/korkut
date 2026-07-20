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
        // TOTP envelope key (base64, 32 bytes). The host builds the ISecretEncryptor
        // singleton at startup (ValidateOnBuild), so every test host needs a valid key.
        Environment.SetEnvironmentVariable("KORKUT_TOTP_KEY", Convert.ToBase64String(new byte[32]));
        // A syntactically-valid placeholder so the host boots for non-DB tests (EF is
        // lazy — it never connects unless a query runs). DB-backed integration tests
        // override this with a real Testcontainers Postgres connection.
        Environment.SetEnvironmentVariable(
            "KORKUT_DB_URL",
            Environment.GetEnvironmentVariable("KORKUT_DB_URL")
                ?? "Host=localhost;Port=1;Database=korkut;Username=placeholder;Password=placeholder");
    }
}

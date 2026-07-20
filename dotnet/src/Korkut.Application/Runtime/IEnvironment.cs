namespace Korkut.Application.Runtime;

/// <summary>
/// Abstraction over process environment variables (KDM-033), so the runtime-config
/// parity layer is unit-testable without touching the real process environment. The
/// production adapter wraps <see cref="System.Environment.GetEnvironmentVariable(string)"/>.
/// </summary>
public interface IEnvironment
{
    string? Get(string name);
}

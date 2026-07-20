using Korkut.Application.Runtime;

namespace Korkut.Infrastructure.Runtime;

/// <summary>Production <see cref="IEnvironment"/> over the real process environment (KDM-033).</summary>
public sealed class SystemEnvironment : IEnvironment
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}

namespace Korkut.Application.Runtime;

/// <summary>A tool's compatibility manifest — its name and the optional API-key env it needs.</summary>
public sealed record ToolManifest(string Name, string? RequiresKey = null);

/// <summary>
/// Tool availability with graceful degradation (KDM-033), ported from the Python
/// <c>core/tool_registry.py</c>: a tool that declares <c>requires_key: &lt;ENV&gt;</c>
/// is HIDDEN when that environment variable is unset, rather than failing the whole
/// runtime. A tool with no key requirement is always available.
/// </summary>
public static class ToolRegistry
{
    public static bool IsAvailable(ToolManifest tool, IEnvironment env)
        => tool.RequiresKey is null || !string.IsNullOrWhiteSpace(env.Get(tool.RequiresKey));

    public static IReadOnlyList<ToolManifest> Available(IEnumerable<ToolManifest> tools, IEnvironment env)
        => tools.Where(t => IsAvailable(t, env)).ToList();
}

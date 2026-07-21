using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Korkut.Application.Runtime;

/// <summary>
/// Parses <c>config/model-routing.yaml</c> into a <see cref="ModelRouting"/> (KDM-033).
/// The YAML shape matches the Python baseline exactly:
/// <code>
/// backends: { cloud: groq, local: ollama }
/// fallback_to_cloud_if_no_local: true
/// specialties: { dispatcher: { model: cloud }, hearing: { model: local }, ... }
/// </code>
/// A missing file or malformed YAML is a fail-fast error (parity with the Python
/// loader), never a silent empty routing.
/// </summary>
public static class ModelRoutingLoader
{
    private sealed class RoutingDoc
    {
        public Dictionary<string, string>? Backends { get; set; }

        [YamlMember(Alias = "fallback_to_cloud_if_no_local")]
        public bool FallbackToCloudIfNoLocal { get; set; }

        public Dictionary<string, SpecialtyDoc>? Specialties { get; set; }
    }

    private sealed class SpecialtyDoc
    {
        public string? Model { get; set; }
    }

    public static ModelRouting Parse(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            throw new InvalidOperationException("model-routing.yaml is empty or missing.");
        }

        // snake_case YAML keys (backends, specialties, fallback_to_cloud_if_no_local)
        // map to PascalCase properties.
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        RoutingDoc doc;
        try
        {
            doc = deserializer.Deserialize<RoutingDoc>(yaml)
                ?? throw new InvalidOperationException("model-routing.yaml parsed to null.");
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new InvalidOperationException($"model-routing.yaml is malformed: {ex.Message}", ex);
        }

        var backends = doc.Backends ?? throw new InvalidOperationException("model-routing.yaml: 'backends' is required.");
        var specialties = (doc.Specialties ?? new())
            .Where(kv => kv.Value?.Model is not null)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.Model!);

        return new ModelRouting(backends, specialties, doc.FallbackToCloudIfNoLocal);
    }

    public static ModelRouting Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"model-routing.yaml not found at '{path}'.", path);
        }

        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Load routing from <c>KORKUT_MODEL_ROUTING_FILE</c> (or the default
    /// <c>config/model-routing.yaml</c>). A MISSING file falls back to a safe default
    /// (all specialties → cloud, cloud=claude, local=ollama) so a host still boots; a
    /// PRESENT-but-malformed file still fails fast (that is operator error, not absence).
    /// </summary>
    public static ModelRouting LoadOrDefault(IEnvironment env)
    {
        var path = env.Get("KORKUT_MODEL_ROUTING_FILE") ?? "config/model-routing.yaml";
        return File.Exists(path) ? Parse(File.ReadAllText(path)) : Default();
    }

    /// <summary>The built-in fallback routing used when no config file is present.</summary>
    public static ModelRouting Default() => new(
        new Dictionary<string, string> { ["cloud"] = "claude", ["local"] = "ollama" },
        new Dictionary<string, string>(),
        fallbackToCloudIfNoLocal: true);
}

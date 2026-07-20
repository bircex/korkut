using System.Text.Json;
using System.Text.Json.Serialization;

namespace Korkut.Infrastructure.Templates;

/// <summary>A built-in template/petition type from the static catalog.</summary>
public sealed record CatalogType(string Id, string Ad, string? Ref, bool Sik);

/// <summary>A category grouping of catalog types.</summary>
public sealed record CatalogCategory(string Kategori, IReadOnlyList<CatalogType> Types);

/// <summary>
/// Loads the static legal document catalog (petition "dilekçe" + contract "sözleşme"
/// types, and the example markdown that serves as each type's default content) from the
/// on-disk <c>templates/</c> tree. Read-only reference data shared across offices — the
/// per-office/user OVERRIDES live in the database (<c>template_overrides</c>).
///
/// The catalog root is <c>KORKUT_TEMPLATES_DIR</c> if set, else the nearest ancestor of
/// the running assembly that contains <c>petition/catalog.json</c>. Parsed once at
/// construction; example files are read on demand.
/// </summary>
public sealed class TemplateCatalog
{
    public const string Dilekce = "dilekce";
    public const string Sozlesme = "sozlesme";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly string _root;
    private readonly Dictionary<string, IReadOnlyList<CatalogCategory>> _catalogs;

    // kind → the on-disk sub-directory holding catalog.json + examples/.
    private static readonly Dictionary<string, string> DirFor = new()
    {
        [Dilekce] = "petition",
        [Sozlesme] = "contract",
    };

    public TemplateCatalog(string? explicitRoot = null)
    {
        _root = explicitRoot
            ?? Environment.GetEnvironmentVariable("KORKUT_TEMPLATES_DIR")
            ?? ResolveRoot()
            ?? throw new InvalidOperationException("Template catalog root not found (set KORKUT_TEMPLATES_DIR).");

        _catalogs = new Dictionary<string, IReadOnlyList<CatalogCategory>>
        {
            [Dilekce] = Load(Dilekce),
            [Sozlesme] = Load(Sozlesme),
        };
    }

    public bool IsKnownKind(string kind) => _catalogs.ContainsKey(kind);

    public IReadOnlyList<CatalogCategory> Categories(string kind) =>
        _catalogs.TryGetValue(kind, out var c) ? c : [];

    public CatalogType? FindType(string kind, string id) =>
        Categories(kind).SelectMany(c => c.Types).FirstOrDefault(t => t.Id == id);

    /// <summary>The built-in default content (example markdown) for a type, or null if none ships.</summary>
    public string? DefaultContent(string kind, string id)
    {
        if (!DirFor.TryGetValue(kind, out var dir))
        {
            return null;
        }

        var path = Path.Combine(_root, dir, "examples", id + ".md");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private List<CatalogCategory> Load(string kind)
    {
        var path = Path.Combine(_root, DirFor[kind], "catalog.json");
        if (!File.Exists(path))
        {
            return [];
        }

        var doc = JsonSerializer.Deserialize<CatalogFile>(File.ReadAllText(path), Json);
        return doc?.Kategoriler?.Select(k => new CatalogCategory(
            k.Kategori ?? "",
            (k.Dilekceler ?? []).Select(t => new CatalogType(
                t.Id ?? "", t.Ad ?? "", t.Mahkeme ?? t.Mevzuat, t.Sik)).ToList())).ToList() ?? [];
    }

    private static string? ResolveRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "templates");
            if (File.Exists(Path.Combine(candidate, "petition", "catalog.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }

    // --- JSON shapes (petition uses "mahkeme", contract uses "mevzuat"; both use "dilekceler") ---

    private sealed class CatalogFile
    {
        [JsonPropertyName("kategoriler")]
        public List<CatalogCategoryJson>? Kategoriler { get; set; }
    }

    private sealed class CatalogCategoryJson
    {
        [JsonPropertyName("kategori")]
        public string? Kategori { get; set; }

        [JsonPropertyName("dilekceler")]
        public List<CatalogTypeJson>? Dilekceler { get; set; }
    }

    private sealed class CatalogTypeJson
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("ad")]
        public string? Ad { get; set; }

        [JsonPropertyName("mahkeme")]
        public string? Mahkeme { get; set; }

        [JsonPropertyName("mevzuat")]
        public string? Mevzuat { get; set; }

        [JsonPropertyName("sik")]
        public bool Sik { get; set; }
    }
}

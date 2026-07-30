using System.Text.Json;
using System.Text.Json.Serialization;

namespace Korkut.Contract.Templates;

/// <summary>A built-in template/petition type from the static catalog.</summary>
public sealed record CatalogType(string Id, string Ad, string? Ref, bool Sik);

/// <summary>A category grouping of catalog types.</summary>
public sealed record CatalogCategory(string Kategori, IReadOnlyList<CatalogType> Types);

/// <summary>How many petition types target a given court.</summary>
public sealed record CourtCoverage(string Label, int Total);

/// <summary>
/// The assistant's document/legal coverage, derived from the static catalog (there is
/// no separate case-law corpus): the count of covered document types, a per-court
/// breakdown of petition types, the source legislation the catalog is built on, and a
/// one-line legislative summary.
/// </summary>
public sealed record CatalogCoverage(int TotalTypes, IReadOnlyList<CourtCoverage> ByCourt, IReadOnlyList<string> Sources, string Legislation);

/// <summary>One section of a document skeleton (petition/contract <c>_base.json</c> <c>bolumler</c>).</summary>
public sealed record TemplateSection(string Id, string? Etiket, bool Zorunlu, string? Hmk, string? Not);

/// <summary>A single evidence item an archetype expects (<c>dava_turleri[].deliller[]</c>).</summary>
public sealed record ArchetypeEvidence(string Ad, string Zorunlu, string? Ispat, bool Celp);

/// <summary>
/// The case archetype a petition type inherits from (<c>dava-workflow.json → dava_turleri</c>):
/// jurisdiction, a procedural precondition, critical deadlines and the expected evidence list.
/// </summary>
public sealed record Archetype(string? Yetki, string? DavaSarti, IReadOnlyList<string> KritikSureler, IReadOnlyList<ArchetypeEvidence> Deliller);

/// <summary>A type-specific intake question (<c>intake/&lt;id&gt;.json → sorular[]</c>).</summary>
public sealed record IntakeQuestion(string Soru, bool Zorunlu);

/// <summary>The type-specific intake schema: pre-drafting questions and post-drafting checks.</summary>
public sealed record IntakeSchema(IReadOnlyList<IntakeQuestion> Sorular, IReadOnlyList<string> Kontroller);

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

    // The HMK m.119 sections a petition may never leave blank (mirrors the Python _PET_CRITICAL).
    private static readonly HashSet<string> PetCritical = ["119/d", "119/e-f", "119/f", "119/g", "119/a"];

    private readonly string _root;
    private readonly Dictionary<string, IReadOnlyList<CatalogCategory>> _catalogs;

    // kind → the document skeleton sections from <kind>/_base.json (drives the drafting format guide).
    private readonly Dictionary<string, IReadOnlyList<TemplateSection>> _sections;

    // catalog category name → the dava-workflow archetype id it inherits (null = no first-instance archetype).
    private readonly Dictionary<string, string?> _categoryToArchetype;

    // archetype id → its inherited jurisdiction/deadlines/evidence (dava_turleri).
    private readonly Dictionary<string, Archetype> _archetypes;

    // type id → its parsed intake schema (or null when the type ships no intake/<id>.json). Filled on demand.
    private readonly Dictionary<string, IntakeSchema?> _intakeCache = [];

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

        _sections = new Dictionary<string, IReadOnlyList<TemplateSection>>
        {
            [Dilekce] = LoadSections(Dilekce),
            [Sozlesme] = LoadSections(Sozlesme),
        };

        (_categoryToArchetype, _archetypes) = LoadWorkflow();
    }

    public bool IsKnownKind(string kind) => _catalogs.ContainsKey(kind);

    /// <summary>
    /// Coverage over the whole catalog (same for every office): total document types,
    /// petition types per court, and the source legislation from mahkemeler.json.
    /// </summary>
    public CatalogCoverage Coverage()
    {
        var totalTypes = _catalogs.Values.Sum(cats => cats.Sum(c => c.Types.Count));
        var byCourt = Categories(Dilekce)
            .SelectMany(c => c.Types)
            .Where(t => !string.IsNullOrWhiteSpace(t.Ref))
            .GroupBy(t => t.Ref!)
            .Select(g => new CourtCoverage(g.Key, g.Count()))
            .OrderByDescending(x => x.Total).ThenBy(x => x.Label)
            .ToList();
        var sources = LoadSources();
        var legislation = sources.Count > 0 ? sources[^1] : string.Empty; // the mahkemeler meta's condensed law list
        return new CatalogCoverage(totalTypes, byCourt, sources, legislation);
    }

    private List<string> LoadSources()
    {
        var path = Path.Combine(_root, "mahkemeler.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("_meta", out var meta) && meta.TryGetProperty("kaynak", out var kaynak)
                && kaynak.ValueKind == JsonValueKind.Array)
            {
                return kaynak.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => s.Length > 0).ToList();
            }
        }
        catch (JsonException)
        {
            // Malformed catalog metadata → no sources; coverage still reports type counts.
        }

        return [];
    }

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

    /// <summary>The kind (<see cref="Dilekce"/>/<see cref="Sozlesme"/>) a type id belongs to, or null.</summary>
    public string? KindOf(string id) =>
        _catalogs.Keys.FirstOrDefault(kind => FindType(kind, id) is not null);

    /// <summary>
    /// The format-aware drafting instruction for a type (ported 1:1 from the Python
    /// <c>petitions.draft_prompt</c> + <c>_intake_block</c>): the ordered sections with their
    /// HMK-critical flags, the Turkish drafting rules, and, for petitions, the case-specific intake
    /// block (inherited jurisdiction/deadlines/evidence from <c>dava-workflow.json</c> plus the
    /// type's own mandatory questions and procedural checks). Null when the type is unknown.
    /// </summary>
    public string? DraftingInstruction(string id)
    {
        var kind = KindOf(id);
        if (kind is null)
        {
            return null;
        }

        var info = FindType(kind, id)!;
        var sections = SectionsText(kind);

        if (kind == Sozlesme)
        {
            return
                $"'{info.Ad}' başlıklı sözleşmeyi hazırla. Dayanak mevzuat: {info.Ref}.\n"
                + "Türk sözleşme formatına uy. Madde dizilimi:\n"
                + $"{sections}\n"
                + "Başlığı ORTALI ve büyük harf yaz. MADDE 1'de tarafları tam unvan + TCKN/VKN + adres ile "
                + "yazıp kısa tanım ata (ör. \"... bundan böyle 'KİRAYA VEREN' olarak anılacaktır\"). "
                + "Maddeleri numaralandır (MADDE 1, 2, 3...; alt bentler 4.1, 4.2). Bedeli rakam+yazı ve KDV "
                + "ile, fesih bildirim süresini, yetkili mahkeme/icra dairelerini, uygulanacak hukuku (Türk "
                + "hukuku) ve nüsha adedini açıkça belirt. Bilgi eksikse köşeli parantezle yer tutucu bırak "
                + "(ör. [Kiracı adı]). Geçerlilik için noter/tapu gibi şekil şartı gerekiyorsa sözleşme "
                + "sonunda kısa bir not olarak uyar.";
        }

        return
            $"'{info.Ad}' hazırla. Görevli/yetkili merci: {info.Ref}.\n"
            + "Türk hukuku dilekçe formatına BİREBİR uy. Bölüm dizilimi ve etiketler:\n"
            + $"{sections}\n"
            + "Mahkeme başlığını büyük harf, datif ve kesme ile yaz (ör. "
            + "\"... NÖBETÇİ ASLİYE HUKUK MAHKEMESİ'NE\"). KRİTİK işaretli bölümler (HMK m.119/a,d,e,f,g) "
            + "asla boş kalmamalı; bilgi eksikse köşeli parantezle yer tutucu bırak (ör. [Davalı adı]). "
            + "AÇIKLAMALAR'da vakıaları sıra numarasıyla (1., 2., 3.) yaz; her vakıaya delil bağla. "
            + "SONUÇ VE İSTEM'de talepleri numaralandır ve yargılama gideri ile vekâlet ücreti talebini ekle."
            + IntakeBlock(kind, id);
    }

    // Render the <kind>/_base.json sections as a labelled, HMK-flagged bullet list (Python _sections).
    private string SectionsText(string kind)
    {
        var fallback = kind == Sozlesme
            ? new Dictionary<string, string> { ["baslik"] = "BAŞLIK", ["imza"] = "İMZALAR" }
            : new Dictionary<string, string> { ["baslik"] = "MAHKEME BAŞLIĞI", ["imza"] = "TARİH / İMZA" };

        var lines = new List<string>();
        foreach (var b in _sections.GetValueOrDefault(kind) ?? [])
        {
            var etiket = !string.IsNullOrWhiteSpace(b.Etiket)
                ? b.Etiket
                : fallback.GetValueOrDefault(b.Id) ?? b.Id.ToUpperInvariant();

            var flag = "";
            if (b.Zorunlu)
            {
                var hmk = b.Hmk ?? "";
                var critical = hmk.Split(',').Any(p => PetCritical.Contains(p.Trim()));
                var kritik = critical ? ", KRİTİK: boş bırakma" : "";
                flag = $"  [ZORUNLU{(hmk.Length > 0 ? " " + hmk : "")}{kritik}]";
            }

            var note = !string.IsNullOrWhiteSpace(b.Not) ? $"  ({b.Not})" : "";
            lines.Add($"- {etiket}{flag}{note}");
        }

        return string.Join("\n", lines);
    }

    // The case-specific intake appendix (Python _intake_block): inherited archetype guidance +
    // the type's own mandatory questions and procedural checks. Petitions only; empty when nothing applies.
    private string IntakeBlock(string kind, string id)
    {
        if (kind != Dilekce)
        {
            return "";
        }

        var parts = new List<string>();

        var arch = ArchetypeFor(id);
        if (arch is not null)
        {
            if (!string.IsNullOrWhiteSpace(arch.Yetki))
            {
                parts.Add($"YETKİ: {arch.Yetki}");
            }

            if (!string.IsNullOrWhiteSpace(arch.DavaSarti))
            {
                parts.Add($"DAVA ŞARTI: {arch.DavaSarti}");
            }

            if (arch.KritikSureler.Count > 0)
            {
                parts.Add("KRİTİK SÜRELER: " + string.Join(" | ", arch.KritikSureler));
            }

            if (arch.Deliller.Count > 0)
            {
                var lines = arch.Deliller.Select(d =>
                {
                    var tag = d.Zorunlu == "true" ? "ZORUNLU" : d.Zorunlu == "kosullu" ? "KOŞULLU" : "destek";
                    var celp = d.Celp ? " (yoksa: mahkemece celbi talep olunur)" : "";
                    return $"  - [{tag}] {d.Ad}: {d.Ispat}{celp}";
                });
                parts.Add(
                    "DELİLLER, bu listeye göre DELİLLER bölümünü kur; mevcut olanları EK no'lu yaz, "
                    + "olmayan ama celbe uygun olanlar için 'mahkemece celbi talep olunur' ekle:\n"
                    + string.Join("\n", lines));
            }
        }

        var intake = IntakeFor(id);
        if (intake is not null)
        {
            var eksik = intake.Sorular.Where(s => s.Zorunlu).Select(s => s.Soru).ToList();
            if (eksik.Count > 0)
            {
                parts.Add(
                    "ZORUNLU BİLGİLER, sohbette verilmemişse köşeli parantezle yer tutucu bırak "
                    + $"(uydurma): {string.Join("; ", eksik)}");
            }

            if (intake.Kontroller.Count > 0)
            {
                parts.Add("USUL KONTROL (yazarken gözet): " + string.Join(" | ", intake.Kontroller));
            }
        }

        return parts.Count == 0 ? "" : "\n\nOLAYA ÖZGÜ YÖNERGE:\n" + string.Join("\n", parts);
    }

    // Resolve a petition type's inherited archetype: id → catalog category → workflow mapping → archetype.
    private Archetype? ArchetypeFor(string id)
    {
        var category = Categories(Dilekce).FirstOrDefault(c => c.Types.Any(t => t.Id == id))?.Kategori;
        if (category is null || !_categoryToArchetype.TryGetValue(category, out var turu) || turu is null)
        {
            return null;
        }

        return _archetypes.GetValueOrDefault(turu);
    }

    // Parse (and cache) intake/<id>.json for a type, or null when it ships none.
    private IntakeSchema? IntakeFor(string id)
    {
        if (_intakeCache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var schema = LoadIntake(id);
        _intakeCache[id] = schema;
        return schema;
    }

    private IntakeSchema? LoadIntake(string id)
    {
        var path = Path.Combine(_root, "intake", id + ".json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            var sorular = new List<IntakeQuestion>();
            if (root.TryGetProperty("sorular", out var qs) && qs.ValueKind == JsonValueKind.Array)
            {
                foreach (var q in qs.EnumerateArray())
                {
                    var soru = q.TryGetProperty("soru", out var s) ? s.GetString() ?? "" : "";
                    var zorunlu = q.TryGetProperty("zorunlu", out var z) && z.ValueKind == JsonValueKind.True;
                    if (soru.Length > 0)
                    {
                        sorular.Add(new IntakeQuestion(soru, zorunlu));
                    }
                }
            }

            var kontroller = StringArray(root, "kontroller");
            return new IntakeSchema(sorular, kontroller);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private List<TemplateSection> LoadSections(string kind)
    {
        var path = Path.Combine(_root, DirFor[kind], "_base.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (!doc.RootElement.TryGetProperty("bolumler", out var bolumler) || bolumler.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var sections = new List<TemplateSection>();
            foreach (var b in bolumler.EnumerateArray())
            {
                var id = b.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                if (id.Length == 0)
                {
                    continue;
                }

                var etiket = b.TryGetProperty("etiket", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
                var zorunlu = b.TryGetProperty("zorunlu", out var z) && z.ValueKind == JsonValueKind.True;
                var hmk = b.TryGetProperty("hmk", out var h) && h.ValueKind == JsonValueKind.String ? h.GetString() : null;
                var not = b.TryGetProperty("not", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                sections.Add(new TemplateSection(id, etiket, zorunlu, hmk, not));
            }

            return sections;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private (Dictionary<string, string?> CategoryMap, Dictionary<string, Archetype> Archetypes) LoadWorkflow()
    {
        var categoryMap = new Dictionary<string, string?>();
        var archetypes = new Dictionary<string, Archetype>();

        var path = Path.Combine(_root, "dava-workflow.json");
        if (!File.Exists(path))
        {
            return (categoryMap, archetypes);
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            if (root.TryGetProperty("kategori_eslesme", out var esl) && esl.TryGetProperty("tablo", out var tablo)
                && tablo.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in tablo.EnumerateObject())
                {
                    categoryMap[entry.Name] = entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString() : null;
                }
            }

            if (root.TryGetProperty("dava_turleri", out var turler) && turler.ValueKind == JsonValueKind.Array)
            {
                foreach (var t in turler.EnumerateArray())
                {
                    var id = t.TryGetProperty("id", out var i) ? i.GetString() ?? "" : "";
                    if (id.Length == 0)
                    {
                        continue;
                    }

                    var yetki = t.TryGetProperty("yetki", out var y) && y.ValueKind == JsonValueKind.String ? y.GetString() : null;
                    var davaSarti = t.TryGetProperty("dava_sarti", out var ds) && ds.ValueKind == JsonValueKind.String ? ds.GetString() : null;
                    var sureler = StringArray(t, "kritik_sureler");

                    var deliller = new List<ArchetypeEvidence>();
                    if (t.TryGetProperty("deliller", out var dl) && dl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var d in dl.EnumerateArray())
                        {
                            var ad = d.TryGetProperty("ad", out var a) ? a.GetString() ?? "" : "";
                            if (ad.Length == 0)
                            {
                                continue;
                            }

                            var zorunlu = d.TryGetProperty("zorunlu", out var z)
                                ? z.ValueKind switch
                                {
                                    JsonValueKind.True => "true",
                                    JsonValueKind.False => "false",
                                    JsonValueKind.String => z.GetString() ?? "false",
                                    _ => "false",
                                }
                                : "false";
                            var ispat = d.TryGetProperty("ispat", out var isp) && isp.ValueKind == JsonValueKind.String ? isp.GetString() : null;
                            var celp = d.TryGetProperty("celp", out var c) && c.ValueKind == JsonValueKind.True;
                            deliller.Add(new ArchetypeEvidence(ad, zorunlu, ispat, celp));
                        }
                    }

                    archetypes[id] = new Archetype(yetki, davaSarti, sureler, deliller);
                }
            }
        }
        catch (JsonException)
        {
            // Malformed workflow → no inheritance; drafting still gets the format sections.
        }

        return (categoryMap, archetypes);
    }

    private static List<string> StringArray(JsonElement obj, string prop)
    {
        if (!obj.TryGetProperty(prop, out var arr) || arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return arr.EnumerateArray()
            .Where(e => e.ValueKind == JsonValueKind.String)
            .Select(e => e.GetString() ?? "")
            .Where(s => s.Length > 0)
            .ToList();
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

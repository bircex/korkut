namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// Veraset ve intikal vergisi (7338 sK). 2026 dilim ve istisna tutarları: Veraset ve İntikal
/// Vergisi Kanunu Genel Tebliği Seri No 57 (RG 31.12.2025, yürürlük 01.01.2026, yeniden
/// değerleme artışı %25,49). Dilimler ve istisnalar ikincil kaynaklardan (narcon.com.tr,
/// TÜRMOB sirküleri) doğrulandı.
/// </summary>
internal static class InheritanceTax2026
{
    public const string Kaynak = "7338 sK Veraset ve İntikal Vergisi Kanunu m.16; 2026 tutarları: VİVK Genel Tebliği Seri No 57 (yürürlük 01.01.2026)";

    public const string Donem = "01.01.2026 - 31.12.2026";

    /// <summary>Veraset yoluyla intikallerde dilim tablosu (2026 matrahları).</summary>
    public static readonly BracketRate[] Veraset =
    [
        new(3_000_000m, 1m),
        new(7_000_000m, 3m),
        new(15_000_000m, 5m),
        new(30_000_000m, 7m),
        new(null, 10m), // 55.000.000 TL üstü
    ];

    /// <summary>İvazsız intikallerde dilim tablosu (2026 matrahları).</summary>
    public static readonly BracketRate[] Ivazsiz =
    [
        new(3_000_000m, 10m),
        new(7_000_000m, 15m),
        new(15_000_000m, 20m),
        new(30_000_000m, 25m),
        new(null, 30m),
    ];

    /// <summary>Evlatlıklar dahil füruğ ve eşten her birine isabet eden miras hissesi istisnası.</summary>
    public const decimal IstisnaFuruVeEs = 2_907_136m;

    /// <summary>Füruğ bulunmaması halinde eşe isabet eden miras hissesi istisnası.</summary>
    public const decimal IstisnaEsTekBasina = 5_817_845m;

    /// <summary>İvazsız intikallerde istisna.</summary>
    public const decimal IstisnaIvazsiz = 66_935m;
}

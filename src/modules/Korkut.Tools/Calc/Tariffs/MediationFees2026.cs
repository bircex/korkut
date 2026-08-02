namespace Korkut.Tools.Calc.Tariffs;

/// <summary>
/// 2026 Yılı Arabuluculuk Asgari Ücret Tarifesi (RG 26.12.2025/33119, yürürlük 01.01.2026).
/// NOT: Saatlik grid ve anlaşma dilimleri iki ikincil kaynaktan çapraz doğrulandı
/// (penbeunerkeskin.av.tr ve saimdogru.com tarife dökümleri); RG ekinin taranmış PDF'i
/// (ADB) üzerinden göz kontrolü hâlâ bekliyor. Birden fazla arabulucu yüzdeleri tek
/// ikincil kaynağa dayanıyor, teyide en muhtaç satırlar onlar.
/// </summary>
public static class MediationFees2026
{
    public const string Kaynak = "2026 Yılı Arabuluculuk Asgari Ücret Tarifesi (RG 26.12.2025/33119)";

    public const string Donem = "01.01.2026 - 31.12.2026";

    /// <summary>
    /// Saatlik ücretler (TL/saat). Dizin: taraf sayısı bandı (0: 2 kişi, 1: 3-5, 2: 6-10, 3: 11+).
    /// </summary>
    public static readonly Dictionary<string, decimal[]> Saatlik = new()
    {
        ["isci_isveren"] = [1_130m, 2_460m, 2_560m, 2_660m],
        ["ticari"] = [1_500m, 3_200m, 3_300m, 3_400m],
        ["tuketici"] = [1_000m, 2_200m, 2_300m, 2_400m],
        ["kira"] = [1_170m, 2_540m, 2_640m, 2_740m],
        ["ortakligin_giderilmesi"] = [1_170m, 2_540m, 2_640m, 2_740m],
        ["diger"] = [1_000m, 2_200m, 2_300m, 2_400m],
    };

    /// <summary>Anlaşma halinde nispi ücret dilimleri, tek arabulucu (Tarife İkinci Kısım).</summary>
    public static readonly BracketRate[] AnlasmaTekArabulucu =
    [
        new(600_000m, 6m),
        new(960_000m, 5m),
        new(1_560_000m, 4m),
        new(3_120_000m, 3m),
        new(9_360_000m, 2m),
        new(12_480_000m, 1.5m),
        new(24_960_000m, 1m),
        new(null, 0.5m), // 53.040.000 TL üstü
    ];

    /// <summary>Anlaşma halinde nispi ücret dilimleri, birden fazla arabulucu.</summary>
    public static readonly BracketRate[] AnlasmaCokluArabulucu =
    [
        new(600_000m, 9m),
        new(960_000m, 7.5m),
        new(1_560_000m, 6m),
        new(3_120_000m, 4.5m),
        new(9_360_000m, 3m),
        new(12_480_000m, 2.5m),
        new(24_960_000m, 1.5m),
        new(null, 1m),
    ];

    /// <summary>Anlaşma halinde asgari ücret (genel).</summary>
    public const decimal AsgariAnlasmaGenel = 9_000m;

    /// <summary>Anlaşma halinde asgari ücret: ticari uyuşmazlıklar ve ortaklığın giderilmesi.</summary>
    public const decimal AsgariAnlasmaTicariOrtaklik = 13_000m;

    /// <summary>Taraf sayısını saatlik ücret bandına çevirir (2 / 3-5 / 6-10 / 11+).</summary>
    public static int TarafBandi(int tarafSayisi) => tarafSayisi switch
    {
        <= 2 => 0,
        <= 5 => 1,
        <= 10 => 2,
        _ => 3,
    };
}

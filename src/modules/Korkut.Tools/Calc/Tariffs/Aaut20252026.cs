namespace Korkut.Tools.Calc.Tariffs;

/// <summary>
/// Avukatlık Asgari Ücret Tarifesi 2025-2026 (RG 04.11.2025/33067, yürürlük 04.11.2025).
/// 2026-2027 tarifesi henüz yayımlanmadı (Ekim-Kasım 2026'da beklenir); çıktığında yeni
/// dosya eklenir, bu sabitler dönem kaydı olarak kalır.
/// </summary>
public static class Aaut20252026
{
    public const string Kaynak = "Avukatlık Asgari Ücret Tarifesi 2025-2026 (RG 04.11.2025/33067)";

    public const string Donem = "04.11.2025'ten itibaren (2025-2026 tarifesi)";

    /// <summary>Konusu para olan davalarda nispi ücret dilimleri (Tarife Üçüncü Kısım).</summary>
    public static readonly BracketRate[] Nispi =
    [
        new(600_000m, 16m),
        new(600_000m, 15m),
        new(1_200_000m, 14m),
        new(1_200_000m, 13m),
        new(1_800_000m, 11m),
        new(2_400_000m, 8m),
        new(3_000_000m, 5m),
        new(3_600_000m, 3m),
        new(4_200_000m, 2m),
        new(null, 1m), // 18.600.000 TL üstü
    ];

    /// <summary>
    /// Maktu ücretler (TL). NOT: fshm (55.000) ve idare_vergi_durusmali (40.000) kalemleri
    /// ikincil kaynaktan (kadimhukuk.com.tr, 2025-2026 tarife dökümü) doğrulandı; RG ekinin
    /// taranmış PDF'i üzerinden göz kontrolü hâlâ bekliyor (bkz. docs/CALC_TOOLS_PLAN.md paragraf 7).
    /// </summary>
    public static readonly Dictionary<string, decimal> Maktu = new()
    {
        ["icra_takibi"] = 9_000m,
        ["tahliyeli_icra_takibi"] = 20_000m,
        ["icra_mahkemesi_durusmali"] = 18_000m,
        ["icra_mahkemesi_durusmasiz"] = 11_000m,
        ["sulh_hukuk"] = 30_000m,
        ["sulh_ceza_infaz"] = 18_000m,
        ["asliye"] = 45_000m,
        ["tuketici"] = 22_500m,
        ["fshm"] = 55_000m,
        ["idare_vergi_durusmasiz"] = 30_000m,
        ["idare_vergi_durusmali"] = 40_000m,
        ["agir_ceza"] = 65_000m,
        ["cocuk"] = 45_000m,
        ["cocuk_agir_ceza"] = 65_000m,
        ["istinaf"] = 22_000m,
        ["yargitay_durusmasiz"] = 22_000m,
        ["yargitay_durusmali"] = 55_000m,
        ["danistay"] = 55_000m,
        ["aym_bireysel_basvuru"] = 55_000m,
    };
}

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
    /// Maktu ücretler (TL). Tamamı RG 04.11.2025/33067 ekinin taranmış tablosundan göz kontrolüyle
    /// birebir doğrulandı (bkz. docs/CALC_TOOLS_PLAN.md paragraf 7, artık teyitli). İstinaf/temyiz/
    /// Danıştay/AYM kalemleri ilk sürümde hatalıydı (tek kalem sanılmıştı); tablo aslında her biri
    /// için ayrı satırlar içeriyor, aşağıda RG'deki İkinci Kısım İkinci Bölüm sıra numaralarıyla.
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
        // RG madde 18: Bölge Adliye/Bölge İdare Mahkemelerinde görülen işlerin takipleri.
        ["bam_ilk_derece"] = 35_000m, // a) İlk derecede görülen davalar
        ["istinaf_tek_durusma"] = 22_000m, // b) İstinaf yolu ile görülen bir duruşması olan işler
        ["istinaf_coklu_durusma"] = 42_000m, // c) Birden fazla duruşma veya keşif gibi işlemler
        ["yargitay_ilk_derece"] = 65_000m, // RG madde 20: Yargıtay'da ilk derecede görülen davalar
        ["danistay_ilk_derece_durusmasiz"] = 40_000m, // RG madde 21-a
        ["danistay_ilk_derece_durusmali"] = 65_000m, // RG madde 21-b
        ["temyiz_durusmali"] = 40_000m, // RG madde 22: Yargıtay/Danıştay/Sayıştay'da temyiz duruşması (tek kalem)
        ["uyusmazlik_mahkemesi"] = 40_000m, // RG madde 23
        ["aym_yuce_divan"] = 120_000m, // RG madde 24-a
        ["aym_bireysel_durusmasiz"] = 40_000m, // RG madde 24-b, duruşmasız
        ["aym_bireysel_durusmali"] = 80_000m, // RG madde 24-b, duruşmalı
        ["aym_diger_dava_isler"] = 90_000m, // RG madde 24-c
    };
}

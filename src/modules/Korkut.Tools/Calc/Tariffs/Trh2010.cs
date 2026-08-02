namespace Korkut.Tools.Calc.Tariffs;

/// <summary>
/// TRH-2010 (Türkiye Kadın-Erkek Hayat Tablosu) bakiye ömür (ex, beklenen kalan yaşam süresi) değerleri.
/// Hacettepe Üniversitesi Aktüerya Bilimleri Bölümü öncülüğünde, BNB Danışmanlık, Marmara ve Başkent
/// Üniversitesi uzmanlarıyla "Türkiye Hayat ve Hayat Annüite Tablolarının Oluşturulması" projesinde
/// üretilmiş, Hazine Müsteşarlığı (bugün SEDDK) tarafından yayımlanmıştır. Yargıtay yerleşik
/// içtihadıyla bedensel zarar ve destekten yoksunluk hesaplarında PMF-1931 yerine esas alınır.
///
/// Veri kaynağı: hukuk pratiğinde yaygın yayımlanan ex tablosu (tazminathukuku.com "TRH-2010 Yaşam
/// Tablosu" ve damlaseyhan.av.tr TRH-2010-YASAM-TABLOSU.pdf ile birebir aynı seri; örnek: yaş 0
/// erkek 71,93 / kadın 78,02; yaş 30 erkek 44,45 / kadın 49,00; yaş 60 erkek 17,62 / kadın 20,79).
/// Tablo 0-99 yaş aralığını tam yaş adımıyla kapsar, interpolasyon gerekmez; yayınlarda 99 yaş için
/// her iki cinsiyette 0,50 verilir ve 99 üzeri yaşlar 99'a kırpılır. Yaş 1 değerinin yaş 0'dan
/// büyük olması bebek ölümlülüğü nedeniyle tablonun kendi özelliğidir, hata değildir.
/// </summary>
public static class Trh2010
{
    public const string Kaynak =
        "TRH-2010 Türkiye Kadın-Erkek Hayat Tablosu (Hacettepe Ü. Aktüerya Bilimleri + BNB, Hazine/SEDDK yayını); ex serisi hukuk pratiğinde yayımlanan tablodan (tazminathukuku.com / damlaseyhan.av.tr)";

    /// <summary>Erkek bakiye ömür (ex), indeks = yaş (0-99).</summary>
    private static readonly decimal[] Erkek =
    [
        71.93m, 72.35m, 71.42m, 70.47m, 69.52m, 68.57m, 67.60m, 66.63m, 65.66m, 64.68m, // 0-9
        63.70m, 62.72m, 61.74m, 60.76m, 59.78m, 58.80m, 57.84m, 56.87m, 55.91m, 54.95m, // 10-19
        53.99m, 53.04m, 52.09m, 51.14m, 50.19m, 49.24m, 48.28m, 47.33m, 46.37m, 45.41m, // 20-29
        44.45m, 43.50m, 42.54m, 41.58m, 40.62m, 39.67m, 38.72m, 37.77m, 36.81m, 35.87m, // 30-39
        34.93m, 33.99m, 33.05m, 32.12m, 31.19m, 30.27m, 29.36m, 28.46m, 27.56m, 26.67m, // 40-49
        25.79m, 24.93m, 24.06m, 23.21m, 22.37m, 21.54m, 20.74m, 19.94m, 19.15m, 18.38m, // 50-59
        17.62m, 16.88m, 16.14m, 15.42m, 14.72m, 14.04m, 13.37m, 12.72m, 12.08m, 11.47m, // 60-69
        10.87m, 10.29m, 9.73m, 9.20m, 8.68m, 8.17m, 7.69m, 7.24m, 6.81m, 6.40m,         // 70-79
        5.99m, 5.59m, 5.23m, 4.90m, 4.57m, 4.25m, 3.93m, 3.64m, 3.37m, 3.12m,           // 80-89
        2.90m, 2.66m, 2.39m, 2.10m, 1.80m, 1.55m, 1.40m, 1.23m, 0.96m, 0.50m,           // 90-99
    ];

    /// <summary>Kadın bakiye ömür (ex), indeks = yaş (0-99).</summary>
    private static readonly decimal[] Kadin =
    [
        78.02m, 77.66m, 76.68m, 75.70m, 74.72m, 73.73m, 72.74m, 71.75m, 70.76m, 69.76m, // 0-9
        68.77m, 67.78m, 66.78m, 65.79m, 64.79m, 63.80m, 62.81m, 61.82m, 60.83m, 59.84m, // 10-19
        58.85m, 57.86m, 56.88m, 55.89m, 54.90m, 53.92m, 52.93m, 51.95m, 50.97m, 49.98m, // 20-29
        49.00m, 48.02m, 47.04m, 46.06m, 45.08m, 44.10m, 43.12m, 42.15m, 41.17m, 40.20m, // 30-39
        39.23m, 38.26m, 37.30m, 36.34m, 35.38m, 34.43m, 33.48m, 32.54m, 31.60m, 30.67m, // 40-49
        29.74m, 28.82m, 27.90m, 26.98m, 26.08m, 25.18m, 24.29m, 23.40m, 22.52m, 21.65m, // 50-59
        20.79m, 19.94m, 19.09m, 18.26m, 17.43m, 16.63m, 15.85m, 15.08m, 14.32m, 13.58m, // 60-69
        12.87m, 12.18m, 11.51m, 10.85m, 10.22m, 9.62m, 9.05m, 8.51m, 8.00m, 7.50m,      // 70-79
        7.01m, 6.55m, 6.12m, 5.71m, 5.32m, 4.93m, 4.54m, 4.20m, 3.88m, 3.59m,           // 80-89
        3.29m, 2.97m, 2.64m, 2.32m, 1.99m, 1.67m, 1.36m, 1.05m, 0.75m, 0.50m,           // 90-99
    ];

    /// <summary>Cinsiyet metnini normalize eder; "erkek" ve "kadin"/"kadın" dışındaki değerlerde null döner.
    /// (InvariantGlobalization altında "ı"/"İ" büyütme tablosunda olmadığından önce elle sadeleştirilir.)</summary>
    public static string? NormalizeCinsiyet(string? cinsiyet)
        => cinsiyet?.Trim().Replace('ı', 'i').Replace('İ', 'I').ToUpperInvariant() switch
        {
            "ERKEK" => "erkek",
            "KADIN" => "kadin",
            _ => null,
        };

    /// <summary>Verilen yaş için bakiye ömür (yıl). Yaş 0-100 aralığı dışıysa null; 99 üzeri 99'a kırpılır.</summary>
    public static decimal? BakiyeOmur(string cinsiyet, int yas)
    {
        if (yas is < 0 or > 100 || NormalizeCinsiyet(cinsiyet) is not { } c)
        {
            return null;
        }

        var table = c == "erkek" ? Erkek : Kadin;
        return table[Math.Min(yas, table.Length - 1)];
    }
}

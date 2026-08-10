namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// 2026 yargı harçları: 492 sayılı Harçlar Kanunu (1) sayılı tarife, tutarlar Harçlar Kanunu
/// Genel Tebliği Seri No 98 (RG 31.12.2025, yürürlük 01.01.2026, yeniden değerleme artışı %18,95).
/// 2027 tutarları çıktığında yeni dosya eklenir.
/// </summary>
internal static class CourtFees2026
{
    public const string Kaynak = "492 sK Harçlar Kanunu (1) sayılı tarife; 2026 tutarları: Harçlar Kanunu Genel Tebliği Seri No 98 (yürürlük 01.01.2026)";

    public const string Donem = "01.01.2026 - 31.12.2026";

    /// <summary>Nispi karar ve ilam harcı: binde 68,31.</summary>
    public const decimal KararIlamNispiOran = 0.06831m;

    /// <summary>Nispi karar ve ilam harcının dava açılışında peşin alınan kısmı: 1/4.</summary>
    public const decimal PesinOran = 0.25m;

    /// <summary>Ölüm ve cismani zarar davalarında açılışta peşin alınan kısım: 1/20.</summary>
    public const decimal OlumCismaniPesinOran = 0.05m;

    public const decimal BasvurmaSulh = 335.20m;

    public const decimal BasvurmaAsliyeIdare = 732.00m;

    public const decimal BasvurmaIcra = 732.00m;

    public const decimal IstinafBasvurma = 2_002.00m;

    public const decimal TemyizBasvurma = 3_608.50m;

    public const decimal MaktuKararIlam = 732.00m;

    /// <summary>Celse harcı: binde 2,27, asgari 411,60 TL.</summary>
    public const decimal CelseOran = 0.00227m;

    public const decimal CelseAsgari = 411.60m;

    public const decimal VekaletnameSuret = 104.00m;

    public const decimal Kesif = 5_188.00m;

    /// <summary>İcra takibinde peşin harç: binde 5.</summary>
    public const decimal IcraPesinOran = 0.005m;

    /// <summary>İcra tahsil harcı, hacizden önce ödeme: yüzde 4,55.</summary>
    public const decimal IcraTahsilHacizdenOnce = 0.0455m;

    /// <summary>İcra tahsil harcı, hacizden sonra satıştan önce ödeme: yüzde 9,10.</summary>
    public const decimal IcraTahsilHacizdenSonra = 0.0910m;

    /// <summary>İcra tahsil harcı, satıştan sonra ödeme: yüzde 11,38.</summary>
    public const decimal IcraTahsilSatistanSonra = 0.1138m;
}

namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// 2026 trafik idari para cezaları. 2918 sK, 7574 sayılı Kanun'la kapsamlı biçimde değiştirildi
/// (TBMM kabul 12.02.2026, RG 27.02.2026/33181 — sadece yıllık yeniden değerleme değil, ceza
/// tutarlarının kendisi yeniden belirlendi). Tutarlar EGM Trafik Başkanlığı'nın resmi "2026 Yılı
/// Trafik İdari Para Ceza Rehberi"nden birebir alınmıştır.
/// </summary>
internal static class TrafficFines2026
{
    public const string Kaynak =
        "2918 sK (7574 sK ile değişik); EGM Trafik Başkanlığı 2026 Yılı Trafik İdari Para Ceza Rehberi (RG 27.02.2026/33181)";

    public const string Donem = "27.02.2026'dan itibaren";

    /// <summary>Peşin ödeme indirimi: tebliğden itibaren 1 ay içinde ödemede %25 (KTK m.115).</summary>
    public const decimal PesinIndirimOrani = 0.25m;

    public sealed record SpeedTier(int UstSinirKmSa, decimal Ceza, string? EkYaptirim);

    /// <summary>Yerleşim yeri İÇİNDE hız aşımı kademeleri (km/s aşım üst sınırı → 2026 ceza).</summary>
    public static readonly SpeedTier[] HizYerlesimIci =
    [
        new(10, 2_000m, null),
        new(15, 4_000m, null),
        new(20, 6_000m, null),
        new(25, 8_000m, null),
        new(35, 12_000m, null),
        new(45, 15_000m, null),
        new(55, 20_000m, "30 gün ehliyet men"),
        new(65, 25_000m, "60 gün ehliyet men"),
        new(int.MaxValue, 30_000m, "90 gün ehliyet men"),
    ];

    /// <summary>Yerleşim yeri DIŞINDA hız aşımı kademeleri (km/s aşım üst sınırı → 2026 ceza).</summary>
    public static readonly SpeedTier[] HizYerlesimDisi =
    [
        new(15, 2_000m, null),
        new(20, 4_000m, null),
        new(25, 6_000m, null),
        new(30, 8_000m, null),
        new(40, 12_000m, null),
        new(50, 15_000m, null),
        new(60, 20_000m, "30 gün ehliyet men"),
        new(70, 25_000m, "60 gün ehliyet men"),
        new(int.MaxValue, 30_000m, "90 gün ehliyet men"),
    ];

    public const decimal EmniyetKemeri = 2_500m;

    /// <summary>Alkollü araç kullanma: 1./2./3. ve sonrası tespit (5 yıl içinde tekrar).</summary>
    public static readonly Dictionary<int, decimal> Alkol = new()
    {
        [1] = 25_000m,
        [2] = 50_000m,
        [3] = 150_000m, // 3. ve sonrası aynı tutar
    };

    public const string AlkolEkYaptirim1 = "6 ay ehliyete el konulması";
    public const string AlkolEkYaptirim2 = "2 yıl ehliyete el konulması";
    public const string AlkolEkYaptirim3 = "5 yıl ehliyete el konulması (her seferinde)";

    /// <summary>Kırmızı ışık ihlali: 1'den 6'ya kadar (1 yıl içinde tekrar), sonrası 6. kademeyle aynı.</summary>
    public static readonly Dictionary<int, decimal> KirmiziIsik = new()
    {
        [1] = 5_000m,
        [2] = 10_000m,
        [3] = 15_000m,
        [4] = 20_000m,
        [5] = 30_000m,
        [6] = 80_000m,
    };

    public const string KirmiziIsik6EkYaptirim = "Ehliyet İPTALİ";

    /// <summary>Cep telefonuyla konuşarak araç kullanma: 1'den 3'e (3. ve sonrası aynı).</summary>
    public static readonly Dictionary<int, decimal> CepTelefonu = new()
    {
        [1] = 5_000m,
        [2] = 10_000m,
        [3] = 20_000m,
    };

    public const string CepTelefonu3EkYaptirim = "Her seferinde 30 gün ehliyet men";

    public const decimal TrafikSigortasiYok = 1_246m;

    public const decimal MuayenesizArac = 2_719m;

    public const decimal MuayenesizAracTekrar = 5_662m;
}

namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// İşsizlik ödeneği parametreleri (4447 sK m.50-51). Oran (%40), tavan (%80) ve süre kademeleri
/// 5763 sK m.15 (15.05.2008) ile Kanun'a yazıldı; o tarihten beri değişmedi — yıllık güncelleme
/// gerektiren tek girdi, tavanın türetildiği brüt asgari ücrettir (bkz. PayrollParameters2026).
/// </summary>
internal static class UnemploymentInsurance
{
    public const string Kaynak =
        "4447 sK m.50-51 (oran/tavan/süre kademeleri 5763 sK m.15 ile 15.05.2008'den beri sabit); tavan brüt asgari ücretin 2026 tutarından türetilir";

    /// <summary>Hak kazanmak için son 3 yılda aranan asgari prim günü.</summary>
    public const int MinPrimGunu = 600;

    /// <summary>Ödenek oranı: son 4 ay prime esas kazanç ortalamasının %40'ı.</summary>
    public const decimal OdenekOrani = 0.40m;

    /// <summary>Tavan: brüt asgari ücretin %80'i.</summary>
    public const decimal TavanOrani = 0.80m;

    public sealed record DurationTier(int MinGun, int MaxGun, int OdenekSuresiGun);

    /// <summary>Son 3 yılda ödenen prim gününe göre ödenek süresi kademeleri.</summary>
    public static readonly DurationTier[] SureKademeleri =
    [
        new(600, 899, 180),
        new(900, 1_079, 240),
        new(1_080, int.MaxValue, 300),
    ];
}

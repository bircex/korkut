namespace Korkut.Tools.Calc.Tariffs;

/// <summary>Bir gelir vergisi dilimi: kümülatif matrahın <see cref="UpTo"/> sınırına kadarki kısmına <see cref="Rate"/> uygulanır.</summary>
public sealed record TaxBracket(decimal UpTo, decimal Rate);

/// <summary>
/// 2026 yılı bordro parametreleri. Kaynaklar:
/// - Asgari ücret 2026: Asgari Ücret Tespit Komisyonu kararı (Aralık 2025); brüt 33.030,00 TL/ay
///   (günlük 1.101,00 TL), net 28.075,50 TL/ay. Tek dönem, tüm yıl geçerli.
/// - SGK işçi payı %14 + işsizlik sigortası işçi payı %1 (5510 sK m.81, 4447 sK m.49).
/// - Prime esas kazanç tavanı: brüt asgari ücret x 7,5 = 247.725,00 TL/ay (5510 sK m.82).
/// - Gelir vergisi 2026 ücret tarifesi (GVK m.103, 2026 için yeniden değerlenen tutarlar):
///   190.000 TL'ye kadar %15; 400.000 TL'ye kadar %20; 1.500.000 TL'ye kadar %27 (ücret
///   gelirlerinde; ücret dışında bu sınır 1.000.000 TL); 5.300.000 TL'ye kadar %35; üstü %40.
/// - Damga vergisi: binde 7,59 (488 sK, (1) sayılı tablo IV/1-b).
/// - Asgari ücret istisnası (7349 sK; GVK m.23/18 ve 488 sK ek m.2): ücretin asgari ücrete
///   isabet eden kısmının gelir vergisi ve damga vergisi istisnadır. Asgari ücretlinin
///   eline net 28.075,50 TL geçer (33.030,00 - %15 işçi kesintisi).
/// </summary>
public static class PayrollParameters2026
{
    public const string Kaynak =
        "5510 sK m.81-82; 4447 sK m.49; GVK m.103 (2026 tarifesi) ve m.23/18; 488 sK IV/1-b ve ek m.2; Asgari Ücret Tespit Komisyonu 2026 kararı";

    /// <summary>Aylık brüt asgari ücret, 2026.</summary>
    public const decimal AsgariUcretBrut = 33_030.00m;

    /// <summary>Aylık net asgari ücret, 2026 (vergi ve damga istisnası sonrası).</summary>
    public const decimal AsgariUcretNet = 28_075.50m;

    /// <summary>SGK işçi payı (%14).</summary>
    public const decimal SgkIsciOrani = 0.14m;

    /// <summary>İşsizlik sigortası işçi payı (%1).</summary>
    public const decimal IssizlikIsciOrani = 0.01m;

    /// <summary>SGK prime esas kazanç aylık tavanı (brüt asgari ücret x 7,5).</summary>
    public const decimal SgkTavanAylik = 247_725.00m;

    /// <summary>Damga vergisi oranı (binde 7,59).</summary>
    public const decimal DamgaOrani = 0.00759m;

    /// <summary>2026 ücret gelirleri gelir vergisi tarifesi (kümülatif matrah sınırları).</summary>
    public static readonly TaxBracket[] UcretTarifesi =
    [
        new(190_000m, 0.15m),
        new(400_000m, 0.20m),
        new(1_500_000m, 0.27m),
        new(5_300_000m, 0.35m),
        new(decimal.MaxValue, 0.40m),
    ];

    /// <summary>Kümülatif matrah üzerinden 2026 ücret tarifesine göre toplam gelir vergisi.</summary>
    public static decimal TarifeVergisi(decimal kumulatifMatrah)
    {
        if (kumulatifMatrah <= 0)
        {
            return 0m;
        }

        decimal tax = 0m;
        decimal floor = 0m;
        foreach (var bracket in UcretTarifesi)
        {
            if (kumulatifMatrah <= floor)
            {
                break;
            }

            tax += (Math.Min(kumulatifMatrah, bracket.UpTo) - floor) * bracket.Rate;
            floor = bracket.UpTo;
        }

        return tax;
    }
}

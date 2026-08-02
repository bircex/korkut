namespace Korkut.Tools.Calc.Tariffs;

/// <summary>
/// Karayolları Motorlu Araçlar Zorunlu Mali Sorumluluk Sigortası (ZMSS, trafik sigortası) sabitleri:
/// 2026 kişi başı teminat limiti ve Genel Şartlar ekindeki araç değer kaybı hesap katsayıları.
/// </summary>
public static class Zmss
{
    /// <summary>2026 kişi başı sağlık/sürekli sakatlık ve ölüm teminat limiti (TL).</summary>
    public const decimal KisiBasiTeminatLimiti2026 = 3_600_000m;

    public const string TeminatKaynak =
        "ZMSS 2026 teminat limitleri (SEDDK duyurusu): kişi başı sakatlık/ölüm 3.600.000 TL";

    /// <summary>Baz değer kaybı oranı: aracın rayiç (piyasa) değerinin %19'u.</summary>
    public const decimal BazDegerKaybiOrani = 0.19m;

    public const string DegerKaybiKaynak =
        "ZMSS Genel Şartları Ek: Değer Kaybı Hesaplaması (RG 04.12.2021/31679 ile eklenen formül); katsayı tabloları ikincil kaynaklarla teyit edildi, birincil ek metniyle ayrıca doğrulanması önerilir";

    /// <summary>Hasar boyutu katsayısı (Genel Şartlar: A1 büyük 0,90 / A2 orta 0,75 / A3 küçük 0,50 / A4 basit 0,25).</summary>
    public static decimal? HasarBoyutuKatsayisi(string hasarBoyutu) => hasarBoyutu switch
    {
        "buyuk" => 0.90m,
        "orta" => 0.75m,
        "kucuk" => 0.50m,
        "basit" => 0.25m,
        _ => null,
    };

    /// <summary>Bir kilometre basamağı ve katsayısı (üst sınır dahil; son basamakta null = sınırsız).</summary>
    public sealed record KmBasamak(int Alt, int? Ust, decimal Katsayi);

    /// <summary>
    /// Kullanılmışlık düzeyi (km) katsayısı tablosu. Plan dokümanındaki tabloyla ve ikincil hukuk
    /// kaynaklarıyla (ör. kadimhukuk.com.tr araç değer kaybı rehberi) birebir aynı; ara basamaklarda
    /// uyuşmazlık görülmedi.
    /// </summary>
    public static readonly KmBasamak[] KmKatsayilari =
    [
        new(0, 14_999, 0.90m),
        new(15_000, 29_999, 0.80m),
        new(30_000, 44_999, 0.60m),
        new(45_000, 59_999, 0.40m),
        new(60_000, 74_999, 0.30m),
        new(75_000, 149_999, 0.20m),
        new(150_000, null, 0.10m),
    ];

    /// <summary>Verilen kilometre için katsayı; negatif kilometrede null.</summary>
    public static decimal? KilometreKatsayisi(int kilometre)
    {
        if (kilometre < 0)
        {
            return null;
        }

        foreach (var tier in KmKatsayilari)
        {
            if (kilometre >= tier.Alt && (tier.Ust is not { } upperBound || kilometre <= upperBound))
            {
                return tier.Katsayi;
            }
        }

        return null;
    }
}

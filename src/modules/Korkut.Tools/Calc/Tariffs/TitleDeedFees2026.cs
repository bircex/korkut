namespace Korkut.Tools.Calc.Tariffs;

/// <summary>
/// Tapu harcı: 492 sK (4) sayılı tarife I-20/a. Gayrimenkul devir ve iktisabında alıcı ve
/// satıcıdan ayrı ayrı binde 20 alınır; oran 2026'da değişmedi. Döner sermaye ücreti TKGM
/// tarafından yıllık belirlenir.
/// </summary>
public static class TitleDeedFees2026
{
    public const string Kaynak = "492 sK Harçlar Kanunu (4) sayılı tarife I-20/a (alıcı ve satıcıdan ayrı ayrı binde 20)";

    public const string Donem = "2026 (oran kanuni, yıl içinde değişmedi)";

    /// <summary>Alıcıdan alınan harç oranı: binde 20.</summary>
    public const decimal AliciOran = 0.020m;

    /// <summary>Satıcıdan alınan harç oranı: binde 20.</summary>
    public const decimal SaticiOran = 0.020m;

    /// <summary>
    /// 2026 tapu döner sermaye ücreti resmi kaynaktan birebir doğrulanamadı; ikincil kaynaklar
    /// büyükşehirlerde yaklaşık 6.700 TL, diğer illerde yaklaşık 3.400 TL, yabancı uyruklu
    /// alıcılarda yaklaşık 20.870 TL göstermektedir. Güncel tutar TKGM'den teyit edilmelidir.
    /// </summary>
    public const string DonerSermayeNotu =
        "Döner sermaye ücreti bu hesaba dahil değildir: 2026 için ikincil kaynaklar büyükşehirlerde yaklaşık 6.700 TL, diğer illerde yaklaşık 3.400 TL göstermektedir; kesin tutarı TKGM'den teyit edin.";
}

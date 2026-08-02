namespace Korkut.Tools.Calc.Tariffs;

/// <summary>
/// Damga vergisi (488 sK, (1) sayılı tablo). Nispi oranlar kanunda sabittir; azami vergi ve
/// maktu tutarlar her yıl tebliğle güncellenir (2026: Damga Vergisi Kanunu Genel Tebliği
/// Seri No 71, yürürlük 01.01.2026, artış %18,95).
/// </summary>
public static class StampDuty2026
{
    public const string Kaynak = "488 sK Damga Vergisi Kanunu (1) sayılı tablo; 2026 azami tutar: Damga Vergisi Kanunu Genel Tebliği Seri No 71 (yürürlük 01.01.2026)";

    public const string Donem = "01.01.2026 - 31.12.2026";

    /// <summary>
    /// Bir kağıttan alınabilecek azami damga vergisi (2026). NOT: 29.115.961,10 TL değeri
    /// birden çok ikincil kaynaktan (narcon.com.tr, faturaport.com) doğrulandı; RG tebliğ
    /// metninden birebir teyit edilmesi önerilir.
    /// </summary>
    public const decimal AzamiVergi = 29_115_961.10m;

    /// <summary>Kağıt türüne göre binde oranlar (488 sK (1) sayılı tablo).</summary>
    public static readonly Dictionary<string, decimal> Binde = new()
    {
        ["sozlesme"] = 9.48m,          // mukavelenameler, taahhütnameler, temliknameler
        ["kira_sozlesmesi"] = 1.89m,   // kira mukavelenameleri (sözleşme süresine göre kira bedeli üzerinden)
        ["ihale_karari"] = 5.69m,      // ihale kanunlarına tabi ihale kararları
        ["ucret_odemesi"] = 7.59m,     // maaş, ücret vb. ödemeler
        ["fesihname"] = 1.89m,         // belli parayı ihtiva eden fesihnameler
    };
}

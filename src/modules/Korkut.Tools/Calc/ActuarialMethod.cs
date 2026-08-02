using Korkut.Tools.Calc.Tariffs;

namespace Korkut.Tools.Calc;

/// <summary>
/// Bedensel zarar ve destekten yoksunluk hesaplarının ortak aktüeryal iskeleti (mahkeme modu):
/// bakiye ömür TRH-2010'dan alınır, %10 artırım + %10 iskonto (progresif rant) birbirini
/// götürdüğünden net teknik faiz %0 kabul edilir ve toplam kayıp yıllık kayıp x yıl olarak
/// hesaplanır. Aktif dönem kaza/olay yaşından 60 yaşına kadardır (bakiye ömürle sınırlı),
/// kalan süre pasif dönemdir.
/// </summary>
public static class ActuarialMethod
{
    /// <summary>Aktif dönemin bittiği yaş (yerleşik uygulama standardı).</summary>
    public const int AktifDonemSonuYas = 60;

    /// <summary>Bir kişinin bakiye ömrünün aktif/pasif ayrımı (yıl, kesirli).</summary>
    public sealed record Donemler(decimal BakiyeOmur, decimal AktifYil, decimal PasifYil);

    /// <summary>TRH-2010'a göre dönemleri hesaplar; cinsiyet/yaş geçersizse null.</summary>
    public static Donemler? Hesapla(string cinsiyet, int yas)
    {
        if (Trh2010.BakiyeOmur(cinsiyet, yas) is not { } bakiyeOmur)
        {
            return null;
        }

        var aktif = yas >= AktifDonemSonuYas ? 0m : Math.Min(AktifDonemSonuYas - yas, bakiyeOmur);
        return new Donemler(bakiyeOmur, aktif, Math.Max(0m, bakiyeOmur - aktif));
    }
}

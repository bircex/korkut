namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// Askerlik ve yurtdışı hizmet borçlanmalarının ortak mekaniği: seçilen günlük prime esas kazanç
/// (5510 sK m.82'deki alt/üst sınır arasında) × gün sayısı × prim oranı. Alt/üst sınır 2026 için
/// <see cref="PayrollParameters2026"/>'daki asgari ücretten türetilir (üst sınır çarpanı 7566 sK
/// m.24 ile 01.01.2026'dan itibaren 9 — bkz. PayrollParameters2026 doc).
/// </summary>
internal static class PensionBuyback2026
{
    /// <summary>Seçilebilecek günlük kazancın alt sınırı (= günlük brüt asgari ücret).</summary>
    public const decimal GunlukAltSinir = 1_101.00m;

    /// <summary>Seçilebilecek günlük kazancın üst sınırı (alt sınır x 9, 5510 sK m.82).</summary>
    public const decimal GunlukUstSinir = 9_909.00m;

    /// <summary>
    /// Askerlik borçlanması prim oranı (5510 sK m.41/1-b, "diğerleri"): 7566 sayılı Kanun m.21 ile
    /// 01.01.2026'dan itibaren %32'den %45'e çıkarıldı (RG 19.12.2025/33112). %32 artık yalnızca
    /// m.41/1-a kapsamındaki doğum/analık borçlanmasında geçerlidir.
    /// </summary>
    public const decimal AskerlikPrimOrani = 0.45m;

    public const string AskerlikKaynak =
        "5510 sK m.41/1-b ve m.82; oran 7566 sK m.21 ile %45'e çıkarıldı (RG 19.12.2025/33112, yürürlük 01.01.2026); üst sınır çarpanı 7566 sK m.24 ile 9 (aynı RG); ödeme borcun tebliğinden itibaren 1 ay içinde tek seferde yapılır (m.41/1), süresinde ödenmeyen borçlanma için yeni başvuru gerekir.";

    /// <summary>Yurtdışı hizmet borçlanması prim oranı (3201 sK m.4): 7186 sK ile 01.08.2019'dan beri %45.</summary>
    public const decimal YurtdisiPrimOrani = 0.45m;

    public const string YurtdisiKaynak =
        "3201 sK m.4 (5754 sK ile 08.05.2008'den itibaren TL/PEK esaslı; oran 7186 sK ile 01.08.2019'dan itibaren %45) ve 5510 sK m.82 (alt/üst sınır); borç, tebliğ tarihinden itibaren 3 ay içinde ödenmezse başvuru geçersiz sayılır ve yeniden başvuru gerekir. Not: 08.05.2008 öncesi başvurular için farklı (USD esaslı) geçiş hükümleri uygulanabilir.";
}

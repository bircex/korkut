namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// Aktüeryal tazminat hesaplarında pasif dönem baz geliri olarak kullanılan, AGİ hariç net asgari
/// ücret. Dönem bazlı tutulur; yeni yıl açıklandığında satır eklenir, araç kodu değişmez.
/// 2026 (tek dönem, tüm yıl): brüt 33.030,00 TL, AGİ hariç net 28.075,50 TL, günlük brüt 1.101,00 TL.
/// </summary>
internal static class MinimumWage
{
    public const string Kaynak =
        "Asgari Ücret Tespit Komisyonu 2026 kararı: brüt 33.030,00 TL/ay, AGİ hariç net 28.075,50 TL/ay";

    /// <summary>Bir takvim dönemi için AGİ hariç aylık net asgari ücret (TL).</summary>
    public sealed record Period(DateOnly From, DateOnly? To, decimal AylikNet);

    /// <summary>AGİ hariç aylık net asgari ücret dönemleri (eski dönemler gerektikçe eklenir).</summary>
    public static readonly Period[] AylikNetDonemleri =
    [
        new(new(2025, 1, 1), new(2026, 1, 1), 22104.67m), // 2025 (tek dönem)
        new(new(2026, 1, 1), null, 28075.50m),            // 2026 (tek dönem, tüm yıl)
    ];

    /// <summary>Yürürlükteki (son dönem) AGİ hariç aylık net asgari ücret.</summary>
    public static decimal GuncelAylikNet => AylikNetDonemleri[^1].AylikNet;

    /// <summary>Yürürlükteki AGİ hariç yıllık net asgari ücret.</summary>
    public static decimal GuncelYillikNet => GuncelAylikNet * 12m;
}

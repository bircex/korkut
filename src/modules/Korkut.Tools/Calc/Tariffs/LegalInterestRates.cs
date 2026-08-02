namespace Korkut.Tools.Calc.Tariffs;

/// <summary>
/// 3095 sayılı Kanunî Faiz ve Temerrüt Faizine İlişkin Kanun oranları, dönem dönem.
/// Yasal faiz 31.07.2026'dan itibaren her yıl "TCMB'nin önceki yıl 31 Aralık reeskont oranının
/// %80'i" formülüne bağlı (7589 sK m.10, RG 31.07.2026/33326) — her yılbaşında yeni satır gerekir.
/// </summary>
public static class LegalInterestRates
{
    public const string Kaynak =
        "3095 sK m.1 (yasal) ve m.2/2 (avans); 7589 sK m.10 (RG 31.07.2026/33326); TCMB reeskont/avans tebliğleri (son: RG 20.12.2025/33113)";

    /// <summary>Yasal (kanuni) faiz, 3095 m.1.</summary>
    public static readonly RatePeriod[] Yasal =
    [
        new(new(2006, 1, 1), new(2024, 6, 1), 9m),     // BKK 2005/9831
        new(new(2024, 6, 1), new(2026, 7, 31), 24m),   // 8485 sayılı CB Kararı (RG 21.05.2024)
        new(new(2026, 7, 31), null, 31m),              // 7589 sK m.10: reeskont %38,75 x 0,80
    ];

    /// <summary>Avans faizi (ticari işlerde temerrüt, 3095 m.2/2), TCMB tebliğleri.</summary>
    /// <remarks>01.12.2023 öncesi dönemler tabloda yok; o tarihten eski başlangıçlar hata döner.</remarks>
    public static readonly RatePeriod[] Avans =
    [
        new(new(2023, 12, 1), new(2023, 12, 23), 41.75m),
        new(new(2023, 12, 23), new(2024, 4, 1), 44.25m),
        new(new(2024, 4, 1), new(2024, 12, 28), 51.75m),
        new(new(2024, 12, 28), new(2025, 3, 8), 49.25m),
        new(new(2025, 3, 8), new(2025, 9, 17), 44.25m),
        new(new(2025, 9, 17), new(2025, 12, 20), 42.25m),
        new(new(2025, 12, 20), null, 39.75m),          // RG 20.12.2025/33113
    ];
}

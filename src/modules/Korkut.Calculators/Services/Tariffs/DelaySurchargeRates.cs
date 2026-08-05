namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// One validity window of the monthly delay-surcharge rate. <paramref name="To"/> is exclusive;
/// null = still in force. Unlike <see cref="RatePeriod"/> the percentage here is MONTHLY
/// (6183 m.51 works in months; fractional months accrue daily at rate/30).
/// </summary>
internal sealed record MonthlyRatePeriod(DateOnly From, DateOnly? To, decimal MonthlyPercent);

/// <summary>
/// 6183 sayılı AATUHK m.51 gecikme zammı AYLIK oranları (vergi, SGK primi ve diğer amme
/// alacakları), dönem dönem. Oran Cumhurbaşkanı Kararı ile değişir; yeni karar çıktığında son
/// satır kapatılıp yeni satır eklenir, araç kodu değişmez. VUK gecikme faizi ve pişmanlık zammı
/// da bu orana bağlıdır.
/// </summary>
internal static class DelaySurchargeRates
{
    public const string Kaynak =
        "6183 sK m.51; oran dönemleri: 1266 (RG 29.06.2019/30816), 1592 (RG 02.10.2019/30906), "
        + "1947 (RG 30.12.2019/30994), 5801 (RG 21.07.2022/31899), 7782 (RG 14.11.2023/32369), "
        + "8484 (RG 21.05.2024/32552) ve 10556 (RG 13.11.2025/33076) sayılı Cumhurbaşkanı Kararları";

    /// <summary>Aylık % oranlar; 01.07.2019 öncesi vadeler tablo dışıdır (araç hata döner).</summary>
    public static readonly MonthlyRatePeriod[] Table =
    [
        new(new(2019, 7, 1), new(2019, 10, 2), 2.5m),    // 1266 sayılı CB Kararı (RG 29.06.2019/30816)
        new(new(2019, 10, 2), new(2019, 12, 30), 2.0m),  // 1592 sayılı CB Kararı (RG 02.10.2019/30906)
        new(new(2019, 12, 30), new(2022, 7, 21), 1.6m),  // 1947 sayılı CB Kararı (RG 30.12.2019/30994)
        new(new(2022, 7, 21), new(2023, 11, 14), 2.5m),  // 5801 sayılı CB Kararı (RG 21.07.2022/31899)
        new(new(2023, 11, 14), new(2024, 5, 21), 3.5m),  // 7782 sayılı CB Kararı (RG 14.11.2023/32369)
        new(new(2024, 5, 21), new(2025, 11, 13), 4.5m),  // 8484 sayılı CB Kararı (RG 21.05.2024/32552)
        new(new(2025, 11, 13), null, 3.7m),              // 10556 sayılı CB Kararı (RG 13.11.2025/33076)
    ];
}

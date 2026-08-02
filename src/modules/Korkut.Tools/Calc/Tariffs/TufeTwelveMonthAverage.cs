namespace Korkut.Tools.Calc.Tariffs;

/// <summary>
/// TÜİK TÜFE "on iki aylık ortalamalara göre değişim" oranı (%), ENDEKS AYINA göre. TBK m.344
/// kira artışının üst sınırıdır: yenileme ayında bir önceki ayın (son açıklanan endeks ayının)
/// oranı uygulanır; örn. Temmuz yenilemesi Haziran endeksini kullanır. TÜİK her endeks ayının
/// oranını izleyen ayın başında açıklar; yeni oran açıklandıkça satır eklenir.
/// </summary>
public static class TufeTwelveMonthAverage
{
    public const string Kaynak =
        "TÜİK Tüketici Fiyat Endeksi, on iki aylık ortalamalara göre değişim oranı (%); TBK m.344";

    /// <summary>One index month's announced 12-month-average CPI change (%).</summary>
    public sealed record MonthRate(int Year, int Month, decimal Percent);

    /// <summary>
    /// Endeks ayı bazında oranlar. Yorumdaki ay, oranın uygulandığı kira yenileme dönemidir
    /// (TÜİK bültenleri + kira artış oranı duyuruları, ay ay teyitli).
    /// </summary>
    public static readonly MonthRate[] Table =
    [
        new(2025, 5, 45.80m),  // Haziran 2025 yenilemeleri (TÜİK Mayıs 2025 bülteni)
        new(2025, 6, 43.23m),  // Temmuz 2025 yenilemeleri (TÜİK Haziran 2025 bülteni)
        new(2025, 7, 41.13m),  // Ağustos 2025 yenilemeleri (TÜİK Temmuz 2025 bülteni)
        new(2025, 8, 39.62m),  // Eylül 2025 yenilemeleri (TÜİK Ağustos 2025 bülteni)
        new(2025, 9, 38.36m),  // Ekim 2025 yenilemeleri (TÜİK Eylül 2025 bülteni)
        new(2025, 10, 37.15m), // Kasım 2025 yenilemeleri (TÜİK Ekim 2025 bülteni)
        new(2025, 11, 35.91m), // Aralık 2025 yenilemeleri (TÜİK Kasım 2025 bülteni)
        new(2025, 12, 34.88m), // Ocak 2026 yenilemeleri (TÜİK Aralık 2025 bülteni)
        new(2026, 1, 33.98m),  // Şubat 2026 yenilemeleri (TÜİK Ocak 2026 bülteni)
        new(2026, 2, 33.39m),  // Mart 2026 yenilemeleri (TÜİK Şubat 2026 bülteni)
        new(2026, 3, 32.82m),  // Nisan 2026 yenilemeleri (TÜİK Mart 2026 bülteni)
        new(2026, 4, 32.43m),  // Mayıs 2026 yenilemeleri (TÜİK Nisan 2026 bülteni)
        new(2026, 5, 32.24m),  // Haziran 2026 yenilemeleri (TÜİK Mayıs 2026 bülteni)
        new(2026, 6, 32.03m),  // Temmuz 2026 yenilemeleri (TÜİK Haziran 2026 bülteni, 03.07.2026)
    ];

    /// <summary>The announced rate for an index month, or null when not (yet) in the table.</summary>
    public static decimal? For(int year, int month) =>
        Table.FirstOrDefault(r => r.Year == year && r.Month == month)?.Percent;
}

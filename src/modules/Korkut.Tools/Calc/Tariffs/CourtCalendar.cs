namespace Korkut.Tools.Calc.Tariffs;

/// <summary>
/// Yargısal süre hesabı için takvim sabitleri. Sabit tarihli ulusal bayram ve genel tatiller
/// (2429 sayılı Kanun) her yıl için geçerlidir; dini bayram günleri yıla göre değişir ve şu an
/// yalnız 2026 yılı tablodadır (Diyanet takvimi: Ramazan Bayramı 20-22 Mart, Kurban Bayramı
/// 27-30 Mayıs). Arife günleri ve 28 Ekim yarım gün tatildir; ihtiyatlı yorumla tam tatil gibi
/// süre uzatır. Yeni yıl geldiğinde dini bayram satırları eklenmelidir.
/// </summary>
public static class CourtCalendar
{
    public const string Kaynak =
        "2429 sK (Ulusal Bayram ve Genel Tatiller); HMK m.93 (son gün tatile rastlarsa ilk iş günü); HMK m.102-104 (adli tatil); İYUK m.8/3 ve m.61 (çalışmaya ara verme); 2026 dini bayram günleri Diyanet takvimi";

    /// <summary>Her yıl sabit tarihli resmi tatiller (ay, gün, ad). 28 Ekim yarım gündür.</summary>
    private static readonly (int Month, int Day, string Name)[] FixedHolidays =
    [
        (1, 1, "Yılbaşı"),
        (4, 23, "23 Nisan Ulusal Egemenlik ve Çocuk Bayramı"),
        (5, 1, "1 Mayıs Emek ve Dayanışma Günü"),
        (5, 19, "19 Mayıs Atatürk'ü Anma, Gençlik ve Spor Bayramı"),
        (7, 15, "15 Temmuz Demokrasi ve Milli Birlik Günü"),
        (8, 30, "30 Ağustos Zafer Bayramı"),
        (10, 28, "28 Ekim (yarım gün)"),
        (10, 29, "29 Ekim Cumhuriyet Bayramı"),
    ];

    /// <summary>2026 dini bayram günleri (arifeler yarım gün).</summary>
    private static readonly (DateOnly Date, string Name)[] Religious2026 =
    [
        (new(2026, 3, 19), "Ramazan Bayramı arifesi (yarım gün)"),
        (new(2026, 3, 20), "Ramazan Bayramı 1. gün"),
        (new(2026, 3, 21), "Ramazan Bayramı 2. gün"),
        (new(2026, 3, 22), "Ramazan Bayramı 3. gün"),
        (new(2026, 5, 26), "Kurban Bayramı arifesi (yarım gün)"),
        (new(2026, 5, 27), "Kurban Bayramı 1. gün"),
        (new(2026, 5, 28), "Kurban Bayramı 2. gün"),
        (new(2026, 5, 29), "Kurban Bayramı 3. gün"),
        (new(2026, 5, 30), "Kurban Bayramı 4. gün"),
    ];

    /// <summary>Dini bayram tablosunun kapsadığı yıllar (yeni yıl eklendikçe genişletilir).</summary>
    public static bool HasReligiousHolidayData(int year) => year == 2026;

    public static bool IsWeekend(DateOnly date)
        => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>Tarih resmi tatilse tatilin adı, değilse null.</summary>
    public static string? HolidayName(DateOnly date)
    {
        foreach (var (month, day, name) in FixedHolidays)
        {
            if (date.Month == month && date.Day == day)
            {
                return name;
            }
        }

        foreach (var (holiday, name) in Religious2026)
        {
            if (date == holiday)
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>Adli tatil / idari yargıda çalışmaya ara verme: her yıl 20 Temmuz - 31 Ağustos.</summary>
    public static bool IsAdliTatil(DateOnly date)
        => (date.Month == 7 && date.Day >= 20) || date.Month == 8;

    /// <summary>Adli tatilin bittiği 31 Ağustos'u izleyen 7. gün (HMK m.104 / İYUK m.8/3 uzaması).</summary>
    public static DateOnly AdliTatilUzamasi(int year) => new DateOnly(year, 8, 31).AddDays(7);
}

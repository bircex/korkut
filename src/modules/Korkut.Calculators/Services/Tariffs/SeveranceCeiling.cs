namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// One validity window of a time-varying TL amount (not a rate). <paramref name="To"/> is
/// exclusive; null = still in force. Same table discipline as <see cref="RatePeriod"/>:
/// updating a tariff means closing the last period and appending a row.
/// </summary>
internal sealed record AmountPeriod(DateOnly From, DateOnly? To, decimal Amount);

/// <summary>
/// Kıdem tazminatı tavanı (1475 sK m.14: en yüksek devlet memuruna ödenecek azami emekli
/// ikramiyesi tutarı). Hazine ve Maliye Bakanlığı Mali ve Sosyal Haklar genelgeleriyle her yıl
/// Ocak ve Temmuz'da güncellenir; son değer Temmuz 2026 genelgesiyle (02.07.2026) 73.729,87 TL.
/// </summary>
internal static class SeveranceCeiling
{
    public const string Kaynak =
        "1475 sK m.14 (kıdem tazminatı ve tavanı); Hazine ve Maliye Bakanlığı Mali ve Sosyal Haklar genelgeleri (son: Temmuz 2026 genelgesi, 01.07.2026 - 31.12.2026 dönemi 73.729,87 TL)";

    /// <summary>Dönem dönem kıdem tazminatı tavanı, TL/yıl.</summary>
    public static readonly AmountPeriod[] Periods =
    [
        new(new(2024, 1, 1), new(2024, 7, 1), 35_058.58m),  // Ocak 2024 genelgesi
        new(new(2024, 7, 1), new(2025, 1, 1), 41_828.42m),  // Temmuz 2024 genelgesi
        new(new(2025, 1, 1), new(2025, 7, 1), 46_655.43m),  // Ocak 2025 genelgesi
        new(new(2025, 7, 1), new(2026, 1, 1), 53_919.68m),  // Temmuz 2025 genelgesi
        new(new(2026, 1, 1), new(2026, 7, 1), 64_948.77m),  // Ocak 2026 genelgesi
        new(new(2026, 7, 1), null, 73_729.87m),             // Temmuz 2026 genelgesi
    ];

    /// <summary>Verilen tarihte geçerli tavan dönemi; tablo kapsamı dışıysa null.</summary>
    public static AmountPeriod? At(DateOnly date)
    {
        foreach (var period in Periods)
        {
            if (date >= period.From && (period.To is not { } to || date < to))
            {
                return period;
            }
        }

        return null;
    }
}

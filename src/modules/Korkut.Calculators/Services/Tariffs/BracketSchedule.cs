namespace Korkut.Calculators.Services.Tariffs;

/// <summary>
/// Kademeli (artan oranlı) bir tarifenin tek dilimi: <paramref name="Size"/> dilimin genişliği (TL),
/// null = tablonun son satırı (üst sınırsız), <paramref name="Percent"/> o dilime uygulanan yüzde.
/// Tarife güncellemesi yalnızca Tariffs dosyasına satır ekler, hesap kodu değişmez.
/// </summary>
internal sealed record BracketRate(decimal? Size, decimal Percent);

/// <summary>
/// Bir tutarı kademeli dilim tablosuna dağıtır (AAÜT nispi ücret, arabuluculuk anlaşma ücreti,
/// veraset ve intikal vergisi gibi artan oranlı hesaplarda ortak yardımcı).
/// </summary>
internal static class BracketSchedule
{
    /// <summary>Bir dilimin uygulanması: dilime düşen tutar, dilim yüzdesi ve hesaplanan pay.</summary>
    public sealed record Slice(decimal DilimTutari, decimal Yuzde, decimal Pay);

    /// <summary>Tutarı sırayla dilimlere dağıtır; yalnızca dolu dilimler döner.</summary>
    public static Slice[] Apply(BracketRate[] table, decimal amount)
    {
        var slices = new List<Slice>(table.Length);
        var remaining = amount;
        foreach (var bracket in table)
        {
            if (remaining <= 0)
            {
                break;
            }

            var inBracket = bracket.Size is { } size && size < remaining ? size : remaining;
            slices.Add(new Slice(inBracket, bracket.Percent, inBracket * bracket.Percent / 100m));
            remaining -= inBracket;
        }

        return [.. slices];
    }

    /// <summary>Dilim paylarının toplamı (yuvarlanmamış).</summary>
    public static decimal Total(Slice[] slices)
    {
        decimal total = 0;
        foreach (var slice in slices)
        {
            total += slice.Pay;
        }

        return total;
    }
}

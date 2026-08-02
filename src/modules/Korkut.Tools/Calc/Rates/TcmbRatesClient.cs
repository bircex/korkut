using System.Globalization;
using System.Net;
using System.Xml.Linq;

namespace Korkut.Tools.Calc.Rates;

/// <summary>One currency row of a TCMB daily bulletin: TL rates per <paramref name="Unit"/> of the currency.</summary>
public sealed record TcmbRate(
    string Code,
    int Unit,
    decimal? ForexBuying,
    decimal? ForexSelling,
    decimal? BanknoteBuying,
    decimal? BanknoteSelling);

/// <summary>A TCMB indicative exchange-rate bulletin: the bulletin's actual date plus its currency rows.</summary>
public sealed record TcmbBulletin(DateOnly Date, IReadOnlyList<TcmbRate> Rates);

/// <summary>
/// Fetches TCMB (Türkiye Cumhuriyet Merkez Bankası) daily indicative exchange-rate bulletins.
/// <c>/kurlar/today.xml</c> is the latest bulletin; historical days live at
/// <c>/kurlar/{yyyyMM}/{ddMMyyyy}.xml</c>. Weekends and holidays have no bulletin (404), so
/// historical lookups fall back to the previous business day, at most 7 steps. No API key.
/// </summary>
public sealed class TcmbRatesClient(IHttpClientFactory httpClientFactory)
{
    /// <summary>Longest 404 fallback walk (covers any weekend + religious-holiday cluster).</summary>
    private const int MaxFallbackSteps = 7;

    /// <summary>The latest published bulletin, or null when the response is not a bulletin.</summary>
    public async Task<TcmbBulletin?> GetLatestAsync(CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("tcmb");
        using var response = await client.GetAsync(new Uri("/kurlar/today.xml", UriKind.Relative), ct);
        response.EnsureSuccessStatusCode();
        return Parse(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>
    /// The bulletin for <paramref name="date"/>, falling back to the previous business day when
    /// the exact day has none. Null when no bulletin is found within the fallback window.
    /// </summary>
    public async Task<TcmbBulletin?> GetForDateAsync(DateOnly date, CancellationToken ct = default)
    {
        var client = httpClientFactory.CreateClient("tcmb");
        var probe = date;
        for (var step = 0; step <= MaxFallbackSteps; step++)
        {
            var path = string.Create(
                CultureInfo.InvariantCulture, $"/kurlar/{probe:yyyyMM}/{probe:ddMMyyyy}.xml");
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative), ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                probe = probe.AddDays(-1);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return Parse(await response.Content.ReadAsStringAsync(ct));
        }

        return null;
    }

    private static TcmbBulletin? Parse(string xml)
    {
        var root = XDocument.Parse(xml).Root;
        if (root is null || !DateOnly.TryParseExact(
                (string?)root.Attribute("Tarih"), "dd.MM.yyyy", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            return null;
        }

        var rates = new List<TcmbRate>();
        foreach (var currency in root.Elements("Currency"))
        {
            var code = (string?)currency.Attribute("Kod");
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            rates.Add(new TcmbRate(
                code,
                int.TryParse((string?)currency.Element("Unit"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var unit) ? unit : 1,
                Dec(currency, "ForexBuying"),
                Dec(currency, "ForexSelling"),
                Dec(currency, "BanknoteBuying"),
                Dec(currency, "BanknoteSelling")));
        }

        return new TcmbBulletin(date, rates);
    }

    private static decimal? Dec(XElement currency, string name) =>
        decimal.TryParse((string?)currency.Element(name), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}

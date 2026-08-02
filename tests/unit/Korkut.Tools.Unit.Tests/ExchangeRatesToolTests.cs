using System.Net;
using System.Text;
using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Calc.Rates;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

/// <summary>
/// TcmbRatesClient + get_exchange_rates against a scripted TCMB endpoint: XML bulletin parsing,
/// the weekend/holiday 404 fallback walk, and the tool's output/error contract. No network; the
/// handler replays canned payloads shaped like real tcmb.gov.tr bulletins.
/// </summary>
public class ExchangeRatesToolTests
{
    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    /// <summary>Scripted transport: replays a canned XML per path, 404 for everything else.</summary>
    private sealed class FakeTcmb : HttpMessageHandler, IHttpClientFactory
    {
        private readonly Dictionary<string, string> _responses = new(StringComparer.OrdinalIgnoreCase);

        public List<string> RequestedPaths { get; } = [];

        public bool ThrowOnSend { get; set; }

        public FakeTcmb On(string path, string xml)
        {
            _responses[path] = xml;
            return this;
        }

        public HttpClient CreateClient(string name)
            => new(this, disposeHandler: false) { BaseAddress = new Uri("https://tcmb.test") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (ThrowOnSend)
            {
                throw new HttpRequestException("connection refused");
            }

            var path = request.RequestUri!.AbsolutePath;
            RequestedPaths.Add(path);
            return Task.FromResult(_responses.TryGetValue(path, out var canned)
                ? new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(canned, Encoding.UTF8, "application/xml"),
                }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private const string SampleBulletin =
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <Tarih_Date Tarih="31.07.2026" Date="07/31/2026" Bulten_No="2026/144">
          <Currency CrossOrder="0" Kod="USD" CurrencyCode="USD">
            <Unit>1</Unit>
            <Isim>ABD DOLARI</Isim>
            <CurrencyName>US DOLLAR</CurrencyName>
            <ForexBuying>41.1234</ForexBuying>
            <ForexSelling>41.1975</ForexSelling>
            <BanknoteBuying>41.0946</BanknoteBuying>
            <BanknoteSelling>41.2593</BanknoteSelling>
          </Currency>
          <Currency CrossOrder="9" Kod="EUR" CurrencyCode="EUR">
            <Unit>1</Unit>
            <Isim>EURO</Isim>
            <CurrencyName>EURO</CurrencyName>
            <ForexBuying>47.5010</ForexBuying>
            <ForexSelling>47.5866</ForexSelling>
            <BanknoteBuying>47.4677</BanknoteBuying>
            <BanknoteSelling>47.6580</BanknoteSelling>
          </Currency>
          <Currency CrossOrder="10" Kod="JPY" CurrencyCode="JPY">
            <Unit>100</Unit>
            <Isim>JAPON YENİ</Isim>
            <CurrencyName>JAPENESE YEN</CurrencyName>
            <ForexBuying>27.1622</ForexBuying>
            <ForexSelling>27.3421</ForexSelling>
            <BanknoteBuying></BanknoteBuying>
            <BanknoteSelling></BanknoteSelling>
          </Currency>
        </Tarih_Date>
        """;

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    // ---- TcmbRatesClient ------------------------------------------------------------------------

    [Fact]
    public async Task Latest_bulletin_parses_date_units_and_optional_rates()
    {
        var api = new FakeTcmb().On("/kurlar/today.xml", SampleBulletin);

        var bulletin = await new TcmbRatesClient(api).GetLatestAsync();

        Assert.NotNull(bulletin);
        Assert.Equal(new DateOnly(2026, 7, 31), bulletin.Date);
        Assert.Equal(3, bulletin.Rates.Count);
        var jpy = bulletin.Rates.Single(r => r.Code == "JPY");
        Assert.Equal(100, jpy.Unit);
        Assert.Equal(27.1622m, jpy.ForexBuying);
        Assert.Null(jpy.BanknoteBuying); // empty element -> no value, not zero
    }

    [Fact]
    public async Task Weekend_date_falls_back_to_the_previous_business_day_bulletin()
    {
        // 02.08.2026 Pazar ve 01.08.2026 Cumartesi bültensiz (404); Cuma 31.07.2026 bülteni var.
        var api = new FakeTcmb().On("/kurlar/202607/31072026.xml", SampleBulletin);

        var bulletin = await new TcmbRatesClient(api).GetForDateAsync(new DateOnly(2026, 8, 2));

        Assert.NotNull(bulletin);
        Assert.Equal(new DateOnly(2026, 7, 31), bulletin.Date); // gerçek bülten tarihi döner
        Assert.Equal(
            ["/kurlar/202608/02082026.xml", "/kurlar/202608/01082026.xml", "/kurlar/202607/31072026.xml"],
            api.RequestedPaths);
    }

    [Fact]
    public async Task More_than_seven_missing_days_returns_null()
    {
        var api = new FakeTcmb(); // her gün 404

        var bulletin = await new TcmbRatesClient(api).GetForDateAsync(new DateOnly(2026, 8, 2));

        Assert.Null(bulletin);
        Assert.Equal(8, api.RequestedPaths.Count); // istenen gün + 7 geri adım
    }

    // ---- get_exchange_rates ---------------------------------------------------------------------

    [Fact]
    public async Task Default_currencies_come_from_the_latest_bulletin_with_source_and_date()
    {
        var api = new FakeTcmb().On("/kurlar/today.xml", SampleBulletin);
        var tool = new ExchangeRatesTool(new TcmbRatesClient(api));

        var result = await tool.InvokeAsync(Args("{}"), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal("2026-07-31", payload.GetProperty("kur_tarihi").GetString());
        Assert.Equal("TCMB Gösterge Kurları", payload.GetProperty("kaynak").GetString());
        var kurlar = payload.GetProperty("kurlar").EnumerateArray().ToList();
        Assert.Equal(2, kurlar.Count); // USD ve EUR bültende, GBP örnek bültende yok
        Assert.Equal("USD", kurlar[0].GetProperty("kod").GetString());
        Assert.Equal(41.1234m, kurlar[0].GetProperty("alis").GetDecimal());
        Assert.Equal(41.1975m, kurlar[0].GetProperty("satis").GetDecimal());
        Assert.Equal(1, kurlar[0].GetProperty("birim").GetInt32());
        Assert.Equal(["GBP"], payload.GetProperty("bulunamayan_kodlar").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Requested_date_uses_the_historical_bulletin_url()
    {
        var api = new FakeTcmb().On("/kurlar/202607/31072026.xml", SampleBulletin);
        var tool = new ExchangeRatesTool(new TcmbRatesClient(api));

        var result = await tool.InvokeAsync(
            Args("""{ "para_birimleri": ["jpy"], "tarih": "2026-07-31" }"""), Ctx);

        Assert.False(result.IsError);
        var kur = Assert.Single(Payload(result).GetProperty("kurlar").EnumerateArray());
        Assert.Equal("JPY", kur.GetProperty("kod").GetString()); // kod normalize edilir
        Assert.Equal(100, kur.GetProperty("birim").GetInt32());
    }

    [Fact]
    public async Task No_requested_currency_in_the_bulletin_is_an_error()
    {
        var api = new FakeTcmb().On("/kurlar/today.xml", SampleBulletin);
        var tool = new ExchangeRatesTool(new TcmbRatesClient(api));

        var result = await tool.InvokeAsync(Args("""{ "para_birimleri": ["XAU"] }"""), Ctx);

        Assert.True(result.IsError);
        Assert.Contains("XAU", result.Content);
    }

    [Fact]
    public async Task An_unreachable_tcmb_is_a_clear_tool_error()
    {
        var api = new FakeTcmb { ThrowOnSend = true };
        var tool = new ExchangeRatesTool(new TcmbRatesClient(api));

        var result = await tool.InvokeAsync(Args("{}"), Ctx);

        Assert.True(result.IsError);
        Assert.Contains("TCMB", result.Content);
    }
}

using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class StatuteOfLimitationsToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Tbk_genel_on_yil()
    {
        var result = await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "tbk_genel", "olay_tarihi": "2020-05-10" }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(10, payload.GetProperty("sure_yil").GetInt32());
        Assert.Equal("2030-05-10", payload.GetProperty("tahmini_son_tarih").GetString());
    }

    [Fact]
    public async Task Haksiz_fiil_iki_ve_on_yil()
    {
        var payload = Payload(await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "haksiz_fiil", "olay_tarihi": "2024-03-15" }"""), Context));

        Assert.Equal(2, payload.GetProperty("sure_yil").GetInt32());
        Assert.Equal("2026-03-15", payload.GetProperty("tahmini_son_tarih").GetString());
        Assert.Equal(10, payload.GetProperty("uzun_sure_yil").GetInt32());
        Assert.Equal("2034-03-15", payload.GetProperty("uzun_sure_son_tarih").GetString());
    }

    [Fact]
    public async Task Kira_bes_yil()
    {
        var payload = Payload(await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "kira", "olay_tarihi": "2023-01-01" }"""), Context));

        Assert.Equal(5, payload.GetProperty("sure_yil").GetInt32());
    }

    [Fact]
    public async Task Ceza_dava_ust_sinira_gore()
    {
        var low = Payload(await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "ceza_dava", "olay_tarihi": "2024-01-01", "ceza_ust_siniri_yil": 4 }"""), Context));
        Assert.Equal(8, low.GetProperty("sure_yil").GetInt32());

        var medium = Payload(await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "ceza_dava", "olay_tarihi": "2024-01-01", "ceza_ust_siniri_yil": 10 }"""), Context));
        Assert.Equal(15, medium.GetProperty("sure_yil").GetInt32());

        var high = Payload(await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "ceza_dava", "olay_tarihi": "2024-01-01", "ceza_ust_siniri_yil": 20 }"""), Context));
        Assert.Equal(20, high.GetProperty("sure_yil").GetInt32());

        var lifeSentence = Payload(await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "ceza_dava", "olay_tarihi": "2024-01-01", "ceza_turu": "muebbet" }"""), Context));
        Assert.Equal(25, lifeSentence.GetProperty("sure_yil").GetInt32());
    }

    [Fact]
    public async Task Ceza_infaz_tck_68()
    {
        var sixYears = Payload(await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "ceza_infaz", "olay_tarihi": "2024-01-01", "ceza_ust_siniri_yil": 6 }"""), Context));
        Assert.Equal(20, sixYears.GetProperty("sure_yil").GetInt32());

        var aggravatedLifeSentence = Payload(await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "ceza_infaz", "olay_tarihi": "2024-01-01", "ceza_turu": "agirlastirilmis_muebbet" }"""), Context));
        Assert.Equal(40, aggravatedLifeSentence.GetProperty("sure_yil").GetInt32());
    }

    [Fact]
    public async Task Kesme_durma_uyarisi_var()
    {
        var payload = Payload(await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "tbk_genel", "olay_tarihi": "2020-05-10" }"""), Context));

        Assert.Contains(payload.GetProperty("uyarilar").EnumerateArray(), u => u.GetString()!.Contains("kesen", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sureli_cezada_ust_sinir_zorunlu()
    {
        var result = await new StatuteOfLimitationsTool().InvokeAsync(Args(
            """{ "alan": "ceza_dava", "olay_tarihi": "2024-01-01" }"""), Context);

        Assert.True(result.IsError);
    }
}

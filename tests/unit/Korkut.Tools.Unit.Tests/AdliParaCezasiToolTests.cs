using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class AdliParaCezasiToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Toplam_ve_hapse_cevirme_hesaplanir()
    {
        var result = await new AdliParaCezasiTool().InvokeAsync(Args(
            """{ "gun_sayisi": 100, "gunluk_miktar": 200 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(20000m, payload.GetProperty("toplam_ceza").GetDecimal());
        Assert.Equal(100, payload.GetProperty("hapse_cevirme_gun").GetInt32());
        Assert.Equal(0, payload.GetProperty("uyarilar").GetArrayLength());
    }

    [Fact]
    public async Task Taksit_istenirse_kural_doner()
    {
        var result = await new AdliParaCezasiTool().InvokeAsync(Args(
            """{ "gun_sayisi": 100, "gunluk_miktar": 200, "taksit": true }"""), Context);

        var kural = Payload(result).GetProperty("taksit_kurali").GetString();
        Assert.NotNull(kural);
        Assert.Contains("5000", kural, StringComparison.Ordinal); // 20000 / 4
    }

    [Fact]
    public async Task Guncel_aralik_disi_gunluk_miktar_uyari_verir()
    {
        var result = await new AdliParaCezasiTool().InvokeAsync(Args(
            """{ "gun_sayisi": 100, "gunluk_miktar": 50 }"""), Context);

        Assert.False(result.IsError);
        Assert.Contains(Payload(result).GetProperty("uyarilar").EnumerateArray(), u => u.GetString()!.Contains("100-500", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Aralik_disi_gun_sayisi_uyari_verir()
    {
        var result = await new AdliParaCezasiTool().InvokeAsync(Args(
            """{ "gun_sayisi": 800, "gunluk_miktar": 200 }"""), Context);

        Assert.Contains(Payload(result).GetProperty("uyarilar").EnumerateArray(), u => u.GetString()!.Contains("5-730", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var result = await new AdliParaCezasiTool().InvokeAsync(Args(
            """{ "gun_sayisi": 0, "gunluk_miktar": 200 }"""), Context);

        Assert.True(result.IsError);
    }
}

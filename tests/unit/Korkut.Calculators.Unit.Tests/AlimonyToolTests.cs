using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class AlimonyToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Yoksulluk_yuzde_20_25_bandi()
    {
        var result = await new AlimonyTool(TestCalc.Service).InvokeAsync(Args(
            """{ "nafaka_turu": "yoksulluk", "yukumlu_net_gelir": 100000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(20000m, payload.GetProperty("tahmini_alt_sinir_tl").GetDecimal());
        Assert.Equal(25000m, payload.GetProperty("tahmini_ust_sinir_tl").GetDecimal());
    }

    [Fact]
    public async Task Istirak_cocuk_basina_yuzde_15_20()
    {
        var payload = Payload(await new AlimonyTool(TestCalc.Service).InvokeAsync(Args(
            """{ "nafaka_turu": "istirak", "yukumlu_net_gelir": 100000, "cocuk_sayisi": 2 }"""), Context));

        Assert.Equal(30000m, payload.GetProperty("tahmini_alt_sinir_tl").GetDecimal());
        Assert.Equal(40000m, payload.GetProperty("tahmini_ust_sinir_tl").GetDecimal());
    }

    [Fact]
    public async Task Toplam_gelirin_yarisini_asamaz()
    {
        var payload = Payload(await new AlimonyTool(TestCalc.Service).InvokeAsync(Args(
            """{ "nafaka_turu": "istirak", "yukumlu_net_gelir": 100000, "cocuk_sayisi": 4 }"""), Context));

        Assert.Equal(50000m, payload.GetProperty("tahmini_ust_sinir_tl").GetDecimal());
        Assert.Equal(50000m, payload.GetProperty("tahmini_alt_sinir_tl").GetDecimal());
        Assert.Contains(payload.GetProperty("uyarilar").EnumerateArray(), u => u.GetString()!.Contains("tavan", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Calisan_es_uyarisi_ve_tahmin_disclaimeri()
    {
        var payload = Payload(await new AlimonyTool(TestCalc.Service).InvokeAsync(Args(
            """{ "nafaka_turu": "yoksulluk", "yukumlu_net_gelir": 100000, "es_calisiyor": true }"""), Context));

        var warnings = payload.GetProperty("uyarilar").EnumerateArray().Select(u => u.GetString()!).ToArray();
        Assert.Contains(warnings, u => u.Contains("çalışıyor", StringComparison.Ordinal));
        Assert.Contains(warnings, u => u.Contains("TAHMİN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Istirakte_cocuk_sayisi_zorunlu()
    {
        var result = await new AlimonyTool(TestCalc.Service).InvokeAsync(Args(
            """{ "nafaka_turu": "istirak", "yukumlu_net_gelir": 100000 }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Gecersiz_gelir_hata_doner()
    {
        var result = await new AlimonyTool(TestCalc.Service).InvokeAsync(Args(
            """{ "nafaka_turu": "yoksulluk", "yukumlu_net_gelir": -1 }"""), Context);

        Assert.True(result.IsError);
    }
}

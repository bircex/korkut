using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class SmmToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Brutten_varsayilan_oranlarla()
    {
        var result = await new SmmTool().InvokeAsync(Args(
            """{ "yon": "brutten", "tutar": 10000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(10000.00m, payload.GetProperty("brut").GetDecimal());
        Assert.Equal(2000.00m, payload.GetProperty("stopaj").GetDecimal());
        Assert.Equal(8000.00m, payload.GetProperty("net_ele_gecen").GetDecimal());
        Assert.Equal(2000.00m, payload.GetProperty("kdv").GetDecimal());
        Assert.Equal(10000.00m, payload.GetProperty("tahsil_edilecek_toplam").GetDecimal());
    }

    [Fact]
    public async Task Kdv_tevkifati_bes_bolu_on_tahsilati_dusurur()
    {
        var result = await new SmmTool().InvokeAsync(Args(
            """{ "yon": "brutten", "tutar": 10000, "kdv_tevkifati": true }"""), Context);

        var payload = Payload(result);
        Assert.Equal(1000.00m, payload.GetProperty("kdv_tevkifati").GetDecimal());
        Assert.Equal(9000.00m, payload.GetProperty("tahsil_edilecek_toplam").GetDecimal());
    }

    [Fact]
    public async Task Netten_brut_geri_hesaplanir()
    {
        // 8.000 / (1 - 0,20) = 10.000
        var result = await new SmmTool().InvokeAsync(Args(
            """{ "yon": "netten", "tutar": 8000 }"""), Context);

        Assert.Equal(10000.00m, Payload(result).GetProperty("brut").GetDecimal());
    }

    [Fact]
    public async Task Kdv_dahilden_brut_geri_hesaplanir()
    {
        // 12.000 / (1 + 0,20) = 10.000
        var result = await new SmmTool().InvokeAsync(Args(
            """{ "yon": "kdv_dahilden", "tutar": 12000 }"""), Context);

        var payload = Payload(result);
        Assert.Equal(10000.00m, payload.GetProperty("brut").GetDecimal());
        Assert.Equal(10000.00m, payload.GetProperty("tahsil_edilecek_toplam").GetDecimal());
    }

    [Fact]
    public async Task Ozel_oranlar_uygulanir()
    {
        // KDV %10, stopaj %17: stopaj 1.700, net 8.300, kdv 1.000
        var result = await new SmmTool().InvokeAsync(Args(
            """{ "yon": "brutten", "tutar": 10000, "kdv_orani": 10, "stopaj_orani": 17 }"""), Context);

        var payload = Payload(result);
        Assert.Equal(1700.00m, payload.GetProperty("stopaj").GetDecimal());
        Assert.Equal(1000.00m, payload.GetProperty("kdv").GetDecimal());
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var sifir = await new SmmTool().InvokeAsync(Args(
            """{ "yon": "brutten", "tutar": 0 }"""), Context);
        var yonsuz = await new SmmTool().InvokeAsync(Args(
            """{ "yon": "tersten", "tutar": 100 }"""), Context);

        Assert.True(sifir.IsError);
        Assert.True(yonsuz.IsError);
    }
}

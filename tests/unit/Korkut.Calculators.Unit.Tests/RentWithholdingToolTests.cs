using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class RentWithholdingToolTests
{
    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Brutten_nete_stopaja_tabiyse_yuzde_20_kesilir()
    {
        var result = await new RentWithholdingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "yon": "brutten_nete", "tutar": 10000, "odeyen_stopaja_tabi": true }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(10000.00m, payload.GetProperty("brut_kira").GetDecimal());
        Assert.Equal(8000.00m, payload.GetProperty("net_kira").GetDecimal());
        Assert.Equal(2000.00m, payload.GetProperty("stopaj").GetDecimal());
        Assert.True(payload.GetProperty("stopaj_uygulandi").GetBoolean());
    }

    [Fact]
    public async Task Netten_brute_stopaja_tabiyse_bolerek_bulunur()
    {
        var result = await new RentWithholdingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "yon": "netten_brute", "tutar": 8000, "odeyen_stopaja_tabi": true }"""), Ctx);

        var payload = Payload(result);
        Assert.Equal(10000.00m, payload.GetProperty("brut_kira").GetDecimal());
        Assert.Equal(8000.00m, payload.GetProperty("net_kira").GetDecimal());
    }

    [Fact]
    public async Task Odeyen_stopaja_tabi_degilse_kesinti_yapilmaz()
    {
        var result = await new RentWithholdingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "yon": "brutten_nete", "tutar": 10000, "odeyen_stopaja_tabi": false }"""), Ctx);

        var payload = Payload(result);
        Assert.Equal(0m, payload.GetProperty("stopaj").GetDecimal());
        Assert.Equal(10000.00m, payload.GetProperty("net_kira").GetDecimal());
        Assert.False(payload.GetProperty("stopaj_uygulandi").GetBoolean());
    }

    [Fact]
    public async Task Gecersiz_tutar_hata_doner()
    {
        var result = await new RentWithholdingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "yon": "brutten_nete", "tutar": 0, "odeyen_stopaja_tabi": true }"""), Ctx);

        Assert.True(result.IsError);
    }
}

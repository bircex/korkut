using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class MilitaryServiceBorrowingToolTests
{
    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Gunluk_tutar_verilmezse_alt_sinir_kullanilir()
    {
        // 1.101,00 x 100 x 0,45 = 49.545,00
        var result = await new MilitaryServiceBorrowingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "gun_sayisi": 100 }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(1101.00m, payload.GetProperty("gunluk_tutar").GetDecimal());
        Assert.Equal(49545.00m, payload.GetProperty("borclanma_tutari").GetDecimal());
        Assert.Equal(0.45m, payload.GetProperty("prim_orani").GetDecimal());
    }

    [Fact]
    public async Task Gecerli_gunluk_tutar_kullanilir()
    {
        // 5.000 x 100 x 0,45 = 225.000,00
        var result = await new MilitaryServiceBorrowingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "gun_sayisi": 100, "gunluk_tutar": 5000 }"""), Ctx);

        Assert.Equal(225000.00m, Payload(result).GetProperty("borclanma_tutari").GetDecimal());
    }

    [Fact]
    public async Task Sinir_disi_gunluk_tutar_hata_doner()
    {
        var result = await new MilitaryServiceBorrowingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "gun_sayisi": 100, "gunluk_tutar": 500 }"""), Ctx);

        Assert.True(result.IsError);
        Assert.Contains("gunluk_tutar", result.Content);
    }

    [Fact]
    public async Task Gecersiz_gun_sayisi_hata_doner()
    {
        var result = await new MilitaryServiceBorrowingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "gun_sayisi": 0 }"""), Ctx);

        Assert.True(result.IsError);
    }
}

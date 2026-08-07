using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class OverseasBorrowingToolTests
{
    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Gunluk_tutar_verilmezse_alt_sinir_kullanilir()
    {
        // 1.101,00 x 200 x 0,45 = 99.090,00
        var result = await new OverseasBorrowingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "gun_sayisi": 200 }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(99090.00m, payload.GetProperty("borclanma_tutari").GetDecimal());
    }

    [Fact]
    public async Task Ust_sinirin_uzerindeki_gunluk_tutar_hata_doner()
    {
        var result = await new OverseasBorrowingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "gun_sayisi": 200, "gunluk_tutar": 10000 }"""), Ctx);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Gecersiz_gun_sayisi_hata_doner()
    {
        var result = await new OverseasBorrowingTool(TestCalc.Service).InvokeAsync(Args(
            """{ "gun_sayisi": -5 }"""), Ctx);

        Assert.True(result.IsError);
    }
}

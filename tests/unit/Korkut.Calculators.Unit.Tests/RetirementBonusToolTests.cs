using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class RetirementBonusToolTests
{
    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Sadece_tam_hizmet_yillari_ikramiyeye_dahil_edilir()
    {
        // 1.000 x 25 tam yıl = 25.000,00; 0,5 yıllık kesir dahil edilmez
        var result = await new RetirementBonusTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ekeat_brut": 1000, "hizmet_yili": 25.5 }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(25m, payload.GetProperty("tam_hizmet_yili").GetDecimal());
        Assert.Equal(0.5m, payload.GetProperty("kesir_yil").GetDecimal());
        Assert.Equal(25000.00m, payload.GetProperty("ikramiye").GetDecimal());
    }

    [Fact]
    public async Task Otuz_yili_asan_hizmette_tavan_uygulanmaz()
    {
        // 1.000 x 32 = 32.000,00 (30 yıllık tavan AYM ile kaldırıldı)
        var result = await new RetirementBonusTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ekeat_brut": 1000, "hizmet_yili": 32 }"""), Ctx);

        Assert.Equal(32000.00m, Payload(result).GetProperty("ikramiye").GetDecimal());
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var result = await new RetirementBonusTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ekeat_brut": 0, "hizmet_yili": 10 }"""), Ctx);
        Assert.True(result.IsError);

        result = await new RetirementBonusTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ekeat_brut": 1000, "hizmet_yili": -1 }"""), Ctx);
        Assert.True(result.IsError);
    }
}

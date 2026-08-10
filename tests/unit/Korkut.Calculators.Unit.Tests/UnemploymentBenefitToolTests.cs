using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class UnemploymentBenefitToolTests
{
    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Hak_kazanan_basvuruda_tavan_altinda_odenek_hesaplanir()
    {
        // Günlük ortalama 96.000/120 = 800; ödenek 800 x %40 = 320,00/gün, tavanın (880,80) altında.
        var result = await new UnemploymentBenefitTool(TestCalc.Service).InvokeAsync(Args(
            """{ "son_120_gun_kesintisiz_calisti": true, "son_3_yilda_prim_gunu": 900, "son_4_ay_toplam_prime_esas_kazanc": 96000 }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.True(payload.GetProperty("hak_kazandi").GetBoolean());
        Assert.Equal(240, payload.GetProperty("odenek_suresi_gun").GetInt32());
        Assert.Equal(320.00m, payload.GetProperty("gunluk_odenek_brut").GetDecimal());
        Assert.Equal(9600.00m, payload.GetProperty("aylik_odenek_brut").GetDecimal());
        Assert.Equal(72.86m, payload.GetProperty("damga_vergisi").GetDecimal());
        Assert.Equal(9527.14m, payload.GetProperty("aylik_odenek_net").GetDecimal());
    }

    [Fact]
    public async Task Yuksek_kazancta_tavan_uygulanir()
    {
        // Günlük ortalama 600.000/120 = 5.000; ödenek 2.000,00/gün, tavana (880,80) düşürülür.
        var result = await new UnemploymentBenefitTool(TestCalc.Service).InvokeAsync(Args(
            """{ "son_120_gun_kesintisiz_calisti": true, "son_3_yilda_prim_gunu": 1200, "son_4_ay_toplam_prime_esas_kazanc": 600000 }"""), Ctx);

        var payload = Payload(result);
        Assert.Equal(300, payload.GetProperty("odenek_suresi_gun").GetInt32());
        Assert.Equal(26424.00m, payload.GetProperty("aylik_odenek_brut").GetDecimal());
        Assert.Equal(26424.00m, payload.GetProperty("aylik_odenek_tavani").GetDecimal());
    }

    [Fact]
    public async Task Son_120_gun_kesintisiz_calisilmadiysa_hak_kazanilmaz()
    {
        var result = await new UnemploymentBenefitTool(TestCalc.Service).InvokeAsync(Args(
            """{ "son_120_gun_kesintisiz_calisti": false, "son_3_yilda_prim_gunu": 900, "son_4_ay_toplam_prime_esas_kazanc": 96000 }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.False(payload.GetProperty("hak_kazandi").GetBoolean());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("odenek_suresi_gun").ValueKind);
    }

    [Fact]
    public async Task Yetersiz_prim_gununde_hak_kazanilmaz()
    {
        var result = await new UnemploymentBenefitTool(TestCalc.Service).InvokeAsync(Args(
            """{ "son_120_gun_kesintisiz_calisti": true, "son_3_yilda_prim_gunu": 500, "son_4_ay_toplam_prime_esas_kazanc": 96000 }"""), Ctx);

        Assert.False(Payload(result).GetProperty("hak_kazandi").GetBoolean());
    }
}

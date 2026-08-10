using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class LatePaymentSurchargeToolTests
{
    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Tam_aylar_tek_donemde_aylik_oranla_hesaplanir()
    {
        // %4,5 dönemi içinde 2 tam ay: 10000 x 0,045 x 2 = 900.
        var result = await new LatePaymentSurchargeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ana_para": 10000, "vade_tarihi": "2025-01-15", "odeme_tarihi": "2025-03-15" }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(2, payload.GetProperty("toplam_tam_ay").GetInt32());
        Assert.Equal(0, payload.GetProperty("toplam_kesir_gun").GetInt32());
        Assert.Equal(900.00m, payload.GetProperty("toplam_zam").GetDecimal());
        Assert.Equal(10900.00m, payload.GetProperty("genel_toplam").GetDecimal());
    }

    [Fact]
    public async Task Ay_kesri_gunluk_oranla_hesaplanir()
    {
        // 10 gün, %4,5: 10000 x 0,045 x 10/30 = 150.
        var result = await new LatePaymentSurchargeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ana_para": 10000, "vade_tarihi": "2025-01-15", "odeme_tarihi": "2025-01-25" }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(0, payload.GetProperty("toplam_tam_ay").GetInt32());
        Assert.Equal(10, payload.GetProperty("toplam_kesir_gun").GetInt32());
        Assert.Equal(150.00m, payload.GetProperty("toplam_zam").GetDecimal());
    }

    [Fact]
    public async Task Oran_degisiminde_donemler_ayri_ayri_hesaplanir()
    {
        // 13.10.2025 - 13.12.2025: bir ay %4,5 (13.10-13.11), bir ay %3,7 (13.11-13.12).
        var result = await new LatePaymentSurchargeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ana_para": 10000, "vade_tarihi": "2025-10-13", "odeme_tarihi": "2025-12-13" }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        var kirilim = payload.GetProperty("donem_kirilimi").EnumerateArray().ToList();
        Assert.Equal(2, kirilim.Count);
        Assert.Equal(4.5m, kirilim[0].GetProperty("aylik_oran").GetDecimal());
        Assert.Equal(450.00m, kirilim[0].GetProperty("zam").GetDecimal());
        Assert.Equal(3.7m, kirilim[1].GetProperty("aylik_oran").GetDecimal());
        Assert.Equal(370.00m, kirilim[1].GetProperty("zam").GetDecimal());
        Assert.Equal(820.00m, payload.GetProperty("toplam_zam").GetDecimal());
    }

    [Fact]
    public async Task Bir_liranin_altindaki_zam_asgari_bir_liraya_tamamlanir()
    {
        // 10 TL, 1 gün, %3,7: 10 x 0,037 / 30 = 0,01 -> asgari 1 TL (m.51/2).
        var result = await new LatePaymentSurchargeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ana_para": 10, "vade_tarihi": "2026-01-05", "odeme_tarihi": "2026-01-06" }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(1.00m, payload.GetProperty("toplam_zam").GetDecimal());
        Assert.Contains("asgari 1 TL", payload.GetProperty("not").GetString());
    }

    [Fact]
    public async Task Tablo_oncesi_vade_hata_doner()
    {
        var result = await new LatePaymentSurchargeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ana_para": 10000, "vade_tarihi": "2019-01-01", "odeme_tarihi": "2020-01-01" }"""), Ctx);

        Assert.True(result.IsError);
        Assert.Contains("01.07.2019", result.Content);
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var result = await new LatePaymentSurchargeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ana_para": -1, "vade_tarihi": "2025-01-01", "odeme_tarihi": "2025-02-01" }"""), Ctx);
        Assert.True(result.IsError);

        result = await new LatePaymentSurchargeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ana_para": 10000, "vade_tarihi": "2025-02-01", "odeme_tarihi": "2025-01-01" }"""), Ctx);
        Assert.True(result.IsError);
    }
}

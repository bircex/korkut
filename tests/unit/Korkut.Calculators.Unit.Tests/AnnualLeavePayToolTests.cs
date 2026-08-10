using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class AnnualLeavePayToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Theory]
    [InlineData(1, 14)]
    [InlineData(5, 14)]
    [InlineData(5.5, 20)]
    [InlineData(14.9, 20)]
    [InlineData(15, 26)]
    [InlineData(25, 26)]
    public async Task Izin_hakki_4857_m53_basamaklarina_uyar(double kidemYil, int beklenenGun)
    {
        var result = await new AnnualLeavePayTool(TestCalc.Service).InvokeAsync(Args(
            $$"""{ "kidem_yil": {{kidemYil}}, "gunluk_brut_ucret": 1500 }"""), Context);

        Assert.False(result.IsError);
        Assert.Equal(beklenenGun, Payload(result).GetProperty("yillik_izin_hakki_gun").GetInt32());
    }

    [Fact]
    public async Task Gun_verilmezse_hak_uzerinden_ucret_hesaplanir()
    {
        // 3 yıl kıdem -> 14 gün x 1.500 = 21.000.
        var result = await new AnnualLeavePayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "kidem_yil": 3, "gunluk_brut_ucret": 1500 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(14m, payload.GetProperty("hesaplanan_gun").GetDecimal());
        Assert.Equal(21000.00m, payload.GetProperty("brut_izin_ucreti").GetDecimal());
    }

    [Fact]
    public async Task Kullanilmayan_gun_verilirse_onunla_hesaplanir()
    {
        var result = await new AnnualLeavePayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "kidem_yil": 3, "gunluk_brut_ucret": 1500, "kullanilmayan_izin_gunu": 30 }"""), Context);

        Assert.False(result.IsError);
        Assert.Equal(45000.00m, Payload(result).GetProperty("brut_izin_ucreti").GetDecimal());
    }

    [Fact]
    public async Task Elli_yas_ve_uzeri_asgari_20_gun()
    {
        var result = await new AnnualLeavePayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "kidem_yil": 3, "gunluk_brut_ucret": 1500, "yas": 52 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(20, payload.GetProperty("yillik_izin_hakki_gun").GetInt32());
        Assert.True(payload.GetProperty("yas_kurali_uygulandi").GetBoolean());
    }

    [Fact]
    public async Task Bir_yildan_az_kidem_hata_doner()
    {
        var result = await new AnnualLeavePayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "kidem_yil": 0.5, "gunluk_brut_ucret": 1500 }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Gecersiz_ucret_hata_doner()
    {
        var result = await new AnnualLeavePayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "kidem_yil": 3, "gunluk_brut_ucret": 0 }"""), Context);

        Assert.True(result.IsError);
    }
}

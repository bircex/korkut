using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class MediationFeeToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Saatlik_ticari_iki_taraf()
    {
        // 3.000 TL/saat x 5 saat = 15.000
        var result = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "ticari", "anlasma": false, "saat": 5 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(3000.00m, payload.GetProperty("saatlik_ucret").GetDecimal());
        Assert.Equal(15000.00m, payload.GetProperty("ucret").GetDecimal());
    }

    [Fact]
    public async Task Aile_hukuku_saatlik_ucreti_tuketiciyle_ayni()
    {
        // RG 26.12.2025/33119 madde 1: aile hukuku, tüketici ve diğer aynı saatlik tutarları paylaşır.
        var result = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "aile", "anlasma": false, "saat": 1 }"""), Context);

        Assert.False(result.IsError);
        Assert.Equal(2000.00m, Payload(result).GetProperty("ucret").GetDecimal());
    }

    [Fact]
    public async Task Tarimsal_saatlik_ucreti_tuketiciyle_ayni()
    {
        var result = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "tarimsal", "anlasma": false, "saat": 1 }"""), Context);

        Assert.False(result.IsError);
        Assert.Equal(2000.00m, Payload(result).GetProperty("ucret").GetDecimal());
    }

    [Fact]
    public async Task Taraf_sayisi_bandi_sinirlari()
    {
        // İşçi-işveren: 5 taraf 3-5 bandında (2.460), 6 taraf 6-10 bandında (2.560).
        var bes = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "isci_isveren", "anlasma": false, "saat": 1, "taraf_sayisi": 5 }"""), Context);
        var alti = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "isci_isveren", "anlasma": false, "saat": 1, "taraf_sayisi": 6 }"""), Context);

        Assert.Equal(2460.00m, Payload(bes).GetProperty("ucret").GetDecimal());
        Assert.Equal(2560.00m, Payload(alti).GetProperty("ucret").GetDecimal());
    }

    [Fact]
    public async Task Anlasma_nispi_iki_dilim()
    {
        // 600.000 x %6 + 400.000 x %5 = 36.000 + 20.000 = 56.000
        var result = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "diger", "anlasma": true, "anlasma_tutari": 1000000 }"""), Context);

        var payload = Payload(result);
        Assert.Equal(56000.00m, payload.GetProperty("ucret").GetDecimal());
        Assert.Equal(2, payload.GetProperty("dilim_kirilimi").GetArrayLength());
        Assert.False(payload.GetProperty("asgari_uygulandi").GetBoolean());
    }

    [Fact]
    public async Task Anlasma_asgari_ucret_tabani_genel()
    {
        // 50.000 x %6 = 3.000 < 9.000
        var result = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "isci_isveren", "anlasma": true, "anlasma_tutari": 50000 }"""), Context);

        var payload = Payload(result);
        Assert.True(payload.GetProperty("asgari_uygulandi").GetBoolean());
        Assert.Equal(9000.00m, payload.GetProperty("ucret").GetDecimal());
    }

    [Fact]
    public async Task Anlasma_asgari_ucret_tabani_ticari()
    {
        // 100.000 x %6 = 6.000 < 13.000 (ticari)
        var result = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "ticari", "anlasma": true, "anlasma_tutari": 100000 }"""), Context);

        Assert.Equal(13000.00m, Payload(result).GetProperty("ucret").GetDecimal());
    }

    [Fact]
    public async Task Ihtiyari_arabuluculukta_asgari_ucret_uygulanmaz()
    {
        // 50.000 x %6 = 3.000; dava_sarti=false olduğu için asgari 9.000 tabanı devre dışı.
        var result = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "isci_isveren", "anlasma": true, "anlasma_tutari": 50000, "dava_sarti": false }"""), Context);

        var payload = Payload(result);
        Assert.False(payload.GetProperty("asgari_uygulandi").GetBoolean());
        Assert.Equal(3000.00m, payload.GetProperty("ucret").GetDecimal());
        Assert.False(payload.GetProperty("dava_sarti").GetBoolean());
    }

    [Fact]
    public async Task Birden_fazla_arabulucuda_yuzde_artar()
    {
        // 600.000 x %9 = 54.000
        var result = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "diger", "anlasma": true, "anlasma_tutari": 600000, "arabulucu_sayisi": 2 }"""), Context);

        Assert.Equal(54000.00m, Payload(result).GetProperty("ucret").GetDecimal());
    }

    [Fact]
    public async Task Anlasma_tutarsiz_veya_saatsiz_cagri_hata_doner()
    {
        var tutarsiz = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "ticari", "anlasma": true }"""), Context);
        var saatsiz = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "ticari", "anlasma": false }"""), Context);

        Assert.True(tutarsiz.IsError);
        Assert.True(saatsiz.IsError);
    }

    [Fact]
    public async Task Bilinmeyen_uyusmazlik_turu_hata_doner()
    {
        var result = await new MediationFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "uyusmazlik_turu": "uzay", "anlasma": false, "saat": 1 }"""), Context);

        Assert.True(result.IsError);
    }
}

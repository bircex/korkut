using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class CourtFeeToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Dava_acilis_nispi_basvurma_arti_pesin_dortte_bir()
    {
        // Başvurma 732,00 + 1.000.000 x 0,06831 x 1/4 = 17.077,50 => 17.809,50
        var result = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "islem": "dava_acilis", "dava_degeri": 1000000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(17809.50m, payload.GetProperty("toplam").GetDecimal());
        Assert.Equal(2, payload.GetProperty("kalemler").GetArrayLength());
    }

    [Fact]
    public async Task Olum_cismani_zarar_davasinda_pesin_yirmide_bir()
    {
        // 732,00 + 1.000.000 x 0,06831 x 1/20 = 3.415,50 => 4.147,50
        var result = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "islem": "dava_acilis", "dava_turu": "olum_cismani", "dava_degeri": 1000000 }"""), Context);

        Assert.Equal(4147.50m, Payload(result).GetProperty("toplam").GetDecimal());
    }

    [Fact]
    public async Task Maktu_dava_acilis_basvurma_arti_maktu_karar_ilam()
    {
        var result = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "islem": "dava_acilis", "maktu": true }"""), Context);

        Assert.Equal(1464.00m, Payload(result).GetProperty("toplam").GetDecimal());
    }

    [Fact]
    public async Task Sulh_mahkemesinde_basvurma_harci_dusuktur()
    {
        var result = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "islem": "dava_acilis", "maktu": true, "mahkeme": "sulh" }"""), Context);

        Assert.Equal(1067.20m, Payload(result).GetProperty("toplam").GetDecimal());
    }

    [Fact]
    public async Task Islah_ek_harci_artis_uzerinden_hesaplanir()
    {
        // (300.000 - 100.000) x 0,06831 x 1/4 = 3.415,50
        var result = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "islem": "islah_deger_artirimi", "eski_deger": 100000, "yeni_deger": 300000 }"""), Context);

        Assert.Equal(3415.50m, Payload(result).GetProperty("toplam").GetDecimal());
    }

    [Fact]
    public async Task Istinaf_ve_temyiz_kalemleri()
    {
        var istinaf = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args("""{ "islem": "istinaf" }"""), Context);
        var temyiz = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args("""{ "islem": "temyiz" }"""), Context);

        Assert.Equal(2734.00m, Payload(istinaf).GetProperty("toplam").GetDecimal());
        Assert.Equal(4340.50m, Payload(temyiz).GetProperty("toplam").GetDecimal());
    }

    [Fact]
    public async Task Icra_takip_basvurma_arti_binde_bes_pesin()
    {
        // 732,00 + 500.000 x 0,005 = 3.232,00
        var result = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "islem": "icra_takip", "dava_degeri": 500000 }"""), Context);

        var payload = Payload(result);
        Assert.Equal(3232.00m, payload.GetProperty("toplam").GetDecimal());
        Assert.Contains("4,55", payload.GetProperty("notlar")[0].GetString());
    }

    [Fact]
    public async Task Islah_yeni_deger_eskiden_kucukse_hata_doner()
    {
        var result = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args(
            """{ "islem": "islah_deger_artirimi", "eski_deger": 300000, "yeni_deger": 100000 }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Nispi_acilis_dava_degeri_olmadan_hata_doner()
    {
        var result = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args("""{ "islem": "dava_acilis" }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Bilinmeyen_islem_hata_doner()
    {
        var result = await new CourtFeeTool(TestCalc.Service).InvokeAsync(Args("""{ "islem": "istirdat" }"""), Context);

        Assert.True(result.IsError);
    }
}

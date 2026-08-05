using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class InheritanceTaxToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Veraset_istisna_dusulur_ilk_dilim_siniri()
    {
        // 5.907.136 - 2.907.136 istisna = 3.000.000 (tam ilk dilim) x %1 = 30.000
        var result = await new InheritanceTaxTool(TestCalc.Service).InvokeAsync(Args(
            """{ "intikal_turu": "veraset", "matrah": 5907136 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(3000000.00m, payload.GetProperty("vergiye_tabi_matrah").GetDecimal());
        Assert.Equal(30000.00m, payload.GetProperty("vergi").GetDecimal());
        Assert.Equal(1, payload.GetProperty("dilim_kirilimi").GetArrayLength());
        Assert.Equal(0.51m, payload.GetProperty("efektif_oran_yuzde").GetDecimal());
    }

    [Fact]
    public async Task Veraset_iki_dilime_yayilan_matrah()
    {
        // Tabi matrah 10.000.000: 3M x %1 + 7M x %3 = 30.000 + 210.000 = 240.000
        var result = await new InheritanceTaxTool(TestCalc.Service).InvokeAsync(Args(
            """{ "intikal_turu": "veraset", "matrah": 12907136 }"""), Context);

        var payload = Payload(result);
        Assert.Equal(240000.00m, payload.GetProperty("vergi").GetDecimal());
        Assert.Equal(2, payload.GetProperty("dilim_kirilimi").GetArrayLength());
    }

    [Fact]
    public async Task Es_tek_basina_istisnasi_daha_yuksektir()
    {
        // Matrah tam istisna kadar: vergiye tabi matrah 0, vergi 0
        var result = await new InheritanceTaxTool(TestCalc.Service).InvokeAsync(Args(
            """{ "intikal_turu": "veraset", "matrah": 5817845, "es_tek_basina": true }"""), Context);

        var payload = Payload(result);
        Assert.Equal(0.00m, payload.GetProperty("vergiye_tabi_matrah").GetDecimal());
        Assert.Equal(0.00m, payload.GetProperty("vergi").GetDecimal());
    }

    [Fact]
    public async Task Ivazsiz_intikal_yuzde_on_ile_baslar()
    {
        // 1.066.935 - 66.935 istisna = 1.000.000 x %10 = 100.000
        var result = await new InheritanceTaxTool(TestCalc.Service).InvokeAsync(Args(
            """{ "intikal_turu": "ivazsiz", "matrah": 1066935 }"""), Context);

        Assert.Equal(100000.00m, Payload(result).GetProperty("vergi").GetDecimal());
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var tur = await new InheritanceTaxTool(TestCalc.Service).InvokeAsync(Args(
            """{ "intikal_turu": "satis", "matrah": 1000000 }"""), Context);
        var matrah = await new InheritanceTaxTool(TestCalc.Service).InvokeAsync(Args(
            """{ "intikal_turu": "veraset", "matrah": -1 }"""), Context);

        Assert.True(tur.IsError);
        Assert.True(matrah.IsError);
    }
}

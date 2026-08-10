using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class TrafficFineToolTests
{
    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Hiz_yerlesim_ici_varsayilan_dogru_kademeyi_bulur()
    {
        // 12 km/s aşım, yerleşim içi varsayılan: 10 kademesini aşar, 15 kademesine düşer -> 4.000,00
        var result = await new TrafficFineTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ihlal_turu": "hiz", "hiz_asim_km_sa": 12 }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(4000.00m, payload.GetProperty("ceza").GetDecimal());
        Assert.Equal(3000.00m, payload.GetProperty("pesin_odeme_indirimli_tutar").GetDecimal());
    }

    [Fact]
    public async Task Hiz_ust_kademede_ehliyet_men_bilgisi_doner()
    {
        // 50 km/s aşım yerleşim içi -> 55 kademesi (20.000,00, 30 gün ehliyet men)
        var result = await new TrafficFineTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ihlal_turu": "hiz", "hiz_asim_km_sa": 50 }"""), Ctx);

        var payload = Payload(result);
        Assert.Equal(20000.00m, payload.GetProperty("ceza").GetDecimal());
        Assert.Equal("30 gün ehliyet men", payload.GetProperty("ek_yaptirim").GetString());
    }

    [Fact]
    public async Task Hiz_asim_verilmezse_hata_doner()
    {
        var result = await new TrafficFineTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ihlal_turu": "hiz" }"""), Ctx);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Emniyet_kemeri_sabit_ceza()
    {
        var result = await new TrafficFineTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ihlal_turu": "emniyet_kemeri" }"""), Ctx);

        Assert.Equal(2500.00m, Payload(result).GetProperty("ceza").GetDecimal());
    }

    [Fact]
    public async Task Alkol_ilk_tespitte_ehliyete_el_konulur()
    {
        var result = await new TrafficFineTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ihlal_turu": "alkol" }"""), Ctx);

        var payload = Payload(result);
        Assert.Equal(25000.00m, payload.GetProperty("ceza").GetDecimal());
        Assert.Contains("6 ay", payload.GetProperty("ek_yaptirim").GetString());
    }

    [Fact]
    public async Task Kirmizi_isik_altinci_tespitte_ehliyet_iptali()
    {
        var result = await new TrafficFineTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ihlal_turu": "kirmizi_isik", "tekrar": 6 }"""), Ctx);

        var payload = Payload(result);
        Assert.Equal(80000.00m, payload.GetProperty("ceza").GetDecimal());
        Assert.Equal("Ehliyet İPTALİ", payload.GetProperty("ek_yaptirim").GetString());
    }

    [Fact]
    public async Task Cep_telefonu_ucuncu_tespitte_kademe_sabitlenir()
    {
        var result = await new TrafficFineTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ihlal_turu": "cep_telefonu", "tekrar": 5 }"""), Ctx);

        Assert.Equal(20000.00m, Payload(result).GetProperty("ceza").GetDecimal());
    }

    [Fact]
    public async Task Muayenesiz_arac_tekrarinda_ceza_artar()
    {
        var ilk = await new TrafficFineTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ihlal_turu": "muayenesiz" }"""), Ctx);
        var tekrar = await new TrafficFineTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ihlal_turu": "muayenesiz", "tekrar": 2 }"""), Ctx);

        Assert.Equal(2719.00m, Payload(ilk).GetProperty("ceza").GetDecimal());
        Assert.Equal(5662.00m, Payload(tekrar).GetProperty("ceza").GetDecimal());
    }

    [Fact]
    public async Task Bilinmeyen_ihlal_turu_hata_doner()
    {
        var result = await new TrafficFineTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ihlal_turu": "asiri_yuk" }"""), Ctx);

        Assert.True(result.IsError);
    }
}

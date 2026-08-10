using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class ReinstatementPayToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Uc_yil_kidem_4_5_ay_bandi()
    {
        var result = await new ReinstatementPayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "aylik_brut": 50000, "kidem_yil": 3 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(200000.00m, payload.GetProperty("bosta_gecen_sure_ucreti").GetDecimal());
        Assert.Equal("4-5 ay", payload.GetProperty("ise_baslatmama_ay_araligi").GetString());
        Assert.Equal(200000.00m, payload.GetProperty("ise_baslatmama_tazminati_min").GetDecimal());
        Assert.Equal(250000.00m, payload.GetProperty("ise_baslatmama_tazminati_max").GetDecimal());
        Assert.Equal(400000.00m, payload.GetProperty("toplam_min").GetDecimal());
        Assert.Equal(450000.00m, payload.GetProperty("toplam_max").GetDecimal());
    }

    [Theory]
    [InlineData(0.3, "4-4 ay")]
    [InlineData(4, "4-5 ay")]
    [InlineData(10, "5-6 ay")]
    [InlineData(20, "6-8 ay")]
    public async Task Kideme_gore_bant_secilir(double kidemYil, string beklenenAralik)
    {
        var result = await new ReinstatementPayTool(TestCalc.Service).InvokeAsync(Args(
            $$"""{ "aylik_brut": 50000, "kidem_yil": {{kidemYil}} }"""), Context);

        Assert.False(result.IsError);
        Assert.Equal(beklenenAralik, Payload(result).GetProperty("ise_baslatmama_ay_araligi").GetString());
    }

    [Fact]
    public async Task Yirmi_yil_kidem_6_8_ay_tutarlari()
    {
        var result = await new ReinstatementPayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "aylik_brut": 50000, "kidem_yil": 20 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(300000.00m, payload.GetProperty("ise_baslatmama_tazminati_min").GetDecimal());
        Assert.Equal(400000.00m, payload.GetProperty("ise_baslatmama_tazminati_max").GetDecimal());
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var result = await new ReinstatementPayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "aylik_brut": -1, "kidem_yil": 3 }"""), Context);

        Assert.True(result.IsError);
    }
}

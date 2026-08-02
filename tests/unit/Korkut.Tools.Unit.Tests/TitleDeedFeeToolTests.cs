using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class TitleDeedFeeToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Toplam_harc_alici_arti_satici_binde_yirmiser()
    {
        var result = await new TitleDeedFeeTool().InvokeAsync(Args(
            """{ "satis_bedeli": 5000000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(100000.00m, payload.GetProperty("alici_harci").GetDecimal());
        Assert.Equal(100000.00m, payload.GetProperty("satici_harci").GetDecimal());
        Assert.Equal(200000.00m, payload.GetProperty("harc").GetDecimal());
    }

    [Fact]
    public async Task Tek_taraf_istendiginde_yalniz_o_taraf_doner()
    {
        var result = await new TitleDeedFeeTool().InvokeAsync(Args(
            """{ "satis_bedeli": 5000000, "taraf": "alici" }"""), Context);

        Assert.Equal(100000.00m, Payload(result).GetProperty("harc").GetDecimal());
    }

    [Fact]
    public async Task Doner_sermaye_notu_ciktida_yer_alir()
    {
        var result = await new TitleDeedFeeTool().InvokeAsync(Args(
            """{ "satis_bedeli": 1000000 }"""), Context);

        Assert.Contains("Döner sermaye", Payload(result).GetProperty("not").GetString());
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var bedel = await new TitleDeedFeeTool().InvokeAsync(Args(
            """{ "satis_bedeli": 0 }"""), Context);
        var taraf = await new TitleDeedFeeTool().InvokeAsync(Args(
            """{ "satis_bedeli": 1000, "taraf": "noter" }"""), Context);

        Assert.True(bedel.IsError);
        Assert.True(taraf.IsError);
    }
}

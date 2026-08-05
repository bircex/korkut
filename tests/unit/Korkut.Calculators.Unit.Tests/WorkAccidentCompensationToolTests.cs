using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class WorkAccidentCompensationToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Isveren_kusuru_ve_sgk_pesin_sermaye_dusulur()
    {
        // Erkek 30: aktif 30, pasif 14,45. Yıllık 480.000, oran %20, işveren kusuru %80.
        var result = await new WorkAccidentCompensationTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 30, "surekli_is_goremezlik_orani": 20, "isveren_kusur_orani": 80, "aylik_net_gelir": 40000, "gecici_is_goremezlik_ay": 3, "sgk_gelir_baglandi": true, "sgk_pesin_sermaye": 100000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(2_880_000.00m, payload.GetProperty("aktif_donem_kaybi").GetDecimal());
        Assert.Equal(973_658.34m, payload.GetProperty("pasif_donem_kaybi").GetDecimal());
        Assert.Equal(120_000.00m, payload.GetProperty("gecici_is_goremezlik_kaybi").GetDecimal());
        Assert.Equal(3_973_658.34m, payload.GetProperty("brut_toplam").GetDecimal());
        Assert.Equal(3_178_926.67m, payload.GetProperty("kusur_sonrasi_tazminat").GetDecimal());
        Assert.Equal(100_000.00m, payload.GetProperty("sgk_pesin_sermaye_indirimi").GetDecimal());
        Assert.Equal(3_078_926.67m, payload.GetProperty("net_tazminat").GetDecimal());
    }

    [Fact]
    public async Task Sgk_geliri_baglandi_ama_tutar_verilmediyse_not_eklenir()
    {
        var result = await new WorkAccidentCompensationTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 40, "surekli_is_goremezlik_orani": 30, "sgk_gelir_baglandi": true }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(0m, payload.GetProperty("sgk_pesin_sermaye_indirimi").GetDecimal());
        Assert.Contains(payload.GetProperty("varsayimlar").EnumerateArray().Select(v => v.GetString()),
            v => v!.Contains("peşin sermaye", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Isveren_kusuru_verilmezse_tam_kusur_varsayilir()
    {
        var result = await new WorkAccidentCompensationTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 30, "surekli_is_goremezlik_orani": 100, "aylik_net_gelir": 30000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(100m, payload.GetProperty("isveren_kusur_orani").GetDecimal());
        // Bedensel araçla aynı iskelet: 10.800.000 + 4.868.291,70.
        Assert.Equal(15_668_291.70m, payload.GetProperty("net_tazminat").GetDecimal());
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var tool = new WorkAccidentCompensationTool(TestCalc.Service);
        Assert.True((await tool.InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 30, "surekli_is_goremezlik_orani": 0 }"""), Context)).IsError);
        Assert.True((await tool.InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 30, "surekli_is_goremezlik_orani": 50, "isveren_kusur_orani": 0 }"""), Context)).IsError);
        Assert.True((await tool.InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 30, "surekli_is_goremezlik_orani": 50, "sgk_pesin_sermaye": -1 }"""), Context)).IsError);
    }
}

using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class BodilyInjuryCompensationToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Tam_maluliyet_aktif_ve_pasif_donem_kaybi()
    {
        // Erkek 30: TRH-2010 ex = 44,45 -> aktif 30 yıl (60'a kadar), pasif 14,45 yıl.
        var result = await new BodilyInjuryCompensationTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 30, "maluliyet_orani": 100, "aylik_net_gelir": 30000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(44.45m, payload.GetProperty("bakiye_omur_yil").GetDecimal());
        Assert.Equal(30m, payload.GetProperty("aktif_donem_yil").GetDecimal());
        Assert.Equal(14.45m, payload.GetProperty("pasif_donem_yil").GetDecimal());
        // Aktif: 30.000 x 12 x 30 = 10.800.000; pasif: 336.906 x 14,45 = 4.868.291,70.
        Assert.Equal(10_800_000.00m, payload.GetProperty("aktif_donem_kaybi").GetDecimal());
        Assert.Equal(4_868_291.70m, payload.GetProperty("pasif_donem_kaybi").GetDecimal());
        Assert.Equal(15_668_291.70m, payload.GetProperty("net_tazminat").GetDecimal());
    }

    [Fact]
    public async Task Kismi_maluliyet_kusur_ve_gecici_donem_dusulur()
    {
        var result = await new BodilyInjuryCompensationTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 30, "maluliyet_orani": 10, "kusur_orani": 25, "aylik_net_gelir": 30000, "gecici_is_goremezlik_ay": 2 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(1_080_000.00m, payload.GetProperty("aktif_donem_kaybi").GetDecimal());
        Assert.Equal(486_829.17m, payload.GetProperty("pasif_donem_kaybi").GetDecimal());
        Assert.Equal(60_000.00m, payload.GetProperty("gecici_is_goremezlik_kaybi").GetDecimal());
        Assert.Equal(1_626_829.17m, payload.GetProperty("brut_toplam").GetDecimal());
        Assert.Equal(406_707.29m, payload.GetProperty("kusur_indirimi").GetDecimal());
        Assert.Equal(1_220_121.88m, payload.GetProperty("net_tazminat").GetDecimal());
    }

    [Fact]
    public async Task Altmis_ustu_yasta_aktif_donem_sifir_ve_asgari_ucret_varsayilir()
    {
        // Kadın 65: ex = 16,63; gelir verilmedi -> AGİ hariç net asgari ücret (28.075,50 TL/ay).
        var result = await new BodilyInjuryCompensationTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "kadin", "yas": 65, "maluliyet_orani": 100 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(0m, payload.GetProperty("aktif_donem_yil").GetDecimal());
        Assert.Equal(0m, payload.GetProperty("aktif_donem_kaybi").GetDecimal());
        Assert.Equal(5_602_746.78m, payload.GetProperty("pasif_donem_kaybi").GetDecimal());
        Assert.Contains("3,600,000.00", payload.GetProperty("zmss_limit_notu").GetString());
        Assert.True(payload.GetProperty("varsayimlar").GetArrayLength() >= 6);
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var tool = new BodilyInjuryCompensationTool(TestCalc.Service);
        Assert.True((await tool.InvokeAsync(Args(
            """{ "cinsiyet": "belirsiz", "yas": 30, "maluliyet_orani": 50 }"""), Context)).IsError);
        Assert.True((await tool.InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 30, "maluliyet_orani": 0 }"""), Context)).IsError);
        Assert.True((await tool.InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 101, "maluliyet_orani": 50 }"""), Context)).IsError);
        Assert.True((await tool.InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 30, "maluliyet_orani": 50, "kusur_orani": 120 }"""), Context)).IsError);
    }
}

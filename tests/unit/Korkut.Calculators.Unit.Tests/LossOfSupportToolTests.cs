using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class LossOfSupportToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Es_ve_iki_cocuk_pay_ve_sure_dagilimi()
    {
        // Müteveffa erkek 40: ex = 34,93 -> aktif 20, pasif 14,93. Paylar: müteveffa 2 + eş 2 + 2 çocuk = 6.
        var result = await new LossOfSupportTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 40, "aylik_net_gelir": 40000, "kusur_orani": 20, "es_var": true, "es_yasi": 38, "cocuklar": [10, 16] }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(34.93m, payload.GetProperty("muteveffa_bakiye_omur_yil").GetDecimal());
        Assert.Equal(6, payload.GetProperty("toplam_pay").GetInt32());

        var sahipler = payload.GetProperty("hak_sahipleri").EnumerateArray().ToArray();
        Assert.Equal(3, sahipler.Length);

        // Eş: kendi bakiye ömrü (kadın 38: 41,17) müteveffanınkinden uzun -> süre 34,93 yıl.
        var es = sahipler[0];
        Assert.Equal("es", es.GetProperty("hak_sahibi").GetString());
        Assert.Equal(34.93m, es.GetProperty("destek_yili").GetDecimal());
        // (480.000 x 20 + 336.906 x 14,93) x 2/6 = 4.876.668,86; kusur %20 sonrası 3.901.335,09.
        Assert.Equal(4_876_668.86m, es.GetProperty("brut_destek").GetDecimal());
        Assert.Equal(3_901_335.09m, es.GetProperty("net_destek").GetDecimal());

        // Çocuklar 18 yaşına kadar: 8 ve 2 yıl, tamamı aktif dönemde.
        Assert.Equal(8m, sahipler[1].GetProperty("destek_yili").GetDecimal());
        Assert.Equal(640_000.00m, sahipler[1].GetProperty("brut_destek").GetDecimal());
        Assert.Equal(2m, sahipler[2].GetProperty("destek_yili").GetDecimal());
        Assert.Equal(160_000.00m, sahipler[2].GetProperty("brut_destek").GetDecimal());

        Assert.Equal(5_676_668.86m, payload.GetProperty("brut_toplam").GetDecimal());
        Assert.Equal(4_541_335.09m, payload.GetProperty("net_toplam").GetDecimal());
    }

    [Fact]
    public async Task Onsekiz_ustu_cocuga_destek_hesaplanmaz()
    {
        var result = await new LossOfSupportTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 45, "cocuklar": [20] }"""), Context);

        Assert.False(result.IsError);
        var cocuk = Payload(result).GetProperty("hak_sahipleri").EnumerateArray().Single();
        Assert.Equal(0m, cocuk.GetProperty("destek_yili").GetDecimal());
        Assert.Equal(0m, cocuk.GetProperty("brut_destek").GetDecimal());
    }

    [Fact]
    public async Task Anne_babaya_kendi_bakiye_omru_kadar_destek()
    {
        // Müteveffa kadın 30 (ex 49,00); anne 70 yaşında (kadın 70: ex 12,87 < 49) -> süre 12,87.
        var result = await new LossOfSupportTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "kadin", "yas": 30, "anne_var": true, "anne_yasi": 70 }"""), Context);

        Assert.False(result.IsError);
        var anne = Payload(result).GetProperty("hak_sahipleri").EnumerateArray().Single();
        Assert.Equal("anne", anne.GetProperty("hak_sahibi").GetString());
        Assert.Equal(12.87m, anne.GetProperty("destek_yili").GetDecimal());
        Assert.Equal(12.87m, anne.GetProperty("aktif_yil").GetDecimal());
    }

    [Fact]
    public async Task Yeniden_evlenme_indirimi_uygulanmadigi_belirtilir()
    {
        var result = await new LossOfSupportTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 40, "es_var": true }"""), Context);

        Assert.False(result.IsError);
        var varsayimlar = Payload(result).GetProperty("varsayimlar").EnumerateArray()
            .Select(v => v.GetString()).ToArray();
        Assert.Contains(varsayimlar, v => v!.Contains("yeniden evlenme", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Hak_sahibi_yoksa_hata_doner()
    {
        var result = await new LossOfSupportTool(TestCalc.Service).InvokeAsync(Args(
            """{ "cinsiyet": "erkek", "yas": 40 }"""), Context);

        Assert.True(result.IsError);
    }
}

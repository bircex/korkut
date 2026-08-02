using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class MirasPayToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    private static JsonElement Mirasci(JsonElement payload, string rol)
    {
        foreach (var m in payload.GetProperty("mirascilar").EnumerateArray())
        {
            if (m.GetProperty("rol").GetString() == rol)
            {
                return m;
            }
        }

        throw new Xunit.Sdk.XunitException($"Mirasçı bulunamadı: {rol}");
    }

    [Fact]
    public async Task Es_ve_iki_cocuk()
    {
        var result = await new MirasPayTool().InvokeAsync(Args(
            """{ "es_var": true, "cocuk_sayisi": 2, "anne_var": false, "baba_var": false, "miras_degeri": 800000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);

        var es = Mirasci(payload, "es");
        Assert.Equal("1/4", es.GetProperty("toplam_pay").GetString());
        Assert.Equal(25m, es.GetProperty("yuzde").GetDecimal());
        Assert.Equal(200000m, es.GetProperty("toplam_tutar_tl").GetDecimal());
        Assert.Equal("1/4", es.GetProperty("sakli_pay").GetString()); // eş: yasal payın tamamı

        var cocuklar = Mirasci(payload, "cocuklar");
        Assert.Equal("3/4", cocuklar.GetProperty("toplam_pay").GetString());
        Assert.Equal("3/8", cocuklar.GetProperty("kisi_basi_pay").GetString());
        Assert.Equal(300000m, cocuklar.GetProperty("kisi_basi_tutar_tl").GetDecimal());
        Assert.Equal("3/8", cocuklar.GetProperty("sakli_pay").GetString()); // altsoy: yasal payın 1/2'si

        Assert.Equal("1", payload.GetProperty("pay_toplami").GetString());
        Assert.Equal("3/8", payload.GetProperty("tasarruf_edilebilir_kisim").GetString());
        Assert.Equal(300000m, payload.GetProperty("tasarruf_edilebilir_tutar_tl").GetDecimal());
    }

    [Fact]
    public async Task Es_anne_ve_baba()
    {
        var payload = Payload(await new MirasPayTool().InvokeAsync(Args(
            """{ "es_var": true, "cocuk_sayisi": 0, "anne_var": true, "baba_var": true }"""), Context));

        Assert.Equal("1/2", Mirasci(payload, "es").GetProperty("toplam_pay").GetString());
        Assert.Equal("1/4", Mirasci(payload, "anne").GetProperty("toplam_pay").GetString());
        Assert.Equal("1/4", Mirasci(payload, "baba").GetProperty("toplam_pay").GetString());
        Assert.Equal("1/16", Mirasci(payload, "anne").GetProperty("sakli_pay").GetString()); // 1/4 x 1/4
        Assert.Equal("3/8", payload.GetProperty("tasarruf_edilebilir_kisim").GetString()); // 1 - (1/2 + 1/16 + 1/16)
    }

    [Fact]
    public async Task Olen_babanin_yerini_kardesler_alir()
    {
        var payload = Payload(await new MirasPayTool().InvokeAsync(Args(
            """{ "es_var": true, "cocuk_sayisi": 0, "anne_var": true, "baba_var": false, "kardes_sayisi": 2 }"""), Context));

        Assert.Equal("1/2", Mirasci(payload, "es").GetProperty("toplam_pay").GetString());
        Assert.Equal("1/4", Mirasci(payload, "anne").GetProperty("toplam_pay").GetString());
        var kardesler = Mirasci(payload, "kardesler");
        Assert.Equal("1/4", kardesler.GetProperty("toplam_pay").GetString());
        Assert.Equal("1/8", kardesler.GetProperty("kisi_basi_pay").GetString());
        Assert.Equal("0", kardesler.GetProperty("sakli_pay").GetString()); // kardeşin saklı payı yok
        Assert.Equal("7/16", payload.GetProperty("tasarruf_edilebilir_kisim").GetString());
    }

    [Fact]
    public async Task Bos_kanat_es_varken_ese_gecer()
    {
        // Anne sağ, baba ölü ve kardeş yok: babanın 1/4 kanadı eşe geçer.
        var payload = Payload(await new MirasPayTool().InvokeAsync(Args(
            """{ "es_var": true, "cocuk_sayisi": 0, "anne_var": true, "baba_var": false }"""), Context));

        Assert.Equal("3/4", Mirasci(payload, "es").GetProperty("toplam_pay").GetString());
        Assert.Equal("1/4", Mirasci(payload, "anne").GetProperty("toplam_pay").GetString());
        Assert.Equal("1", payload.GetProperty("pay_toplami").GetString());
    }

    [Fact]
    public async Task Yalniz_es_tamamini_alir()
    {
        var payload = Payload(await new MirasPayTool().InvokeAsync(Args(
            """{ "es_var": true, "cocuk_sayisi": 0, "anne_var": false, "baba_var": false }"""), Context));

        var es = Mirasci(payload, "es");
        Assert.Equal("1", es.GetProperty("toplam_pay").GetString());
        Assert.Equal("3/4", es.GetProperty("sakli_pay").GetString()); // tek başına: yasal payın 3/4'ü
        Assert.Equal("1/4", payload.GetProperty("tasarruf_edilebilir_kisim").GetString());
    }

    [Fact]
    public async Task Es_yokken_cocuklar_esit_bolusur()
    {
        var payload = Payload(await new MirasPayTool().InvokeAsync(Args(
            """{ "es_var": false, "cocuk_sayisi": 3, "anne_var": false, "baba_var": false }"""), Context));

        var cocuklar = Mirasci(payload, "cocuklar");
        Assert.Equal("1", cocuklar.GetProperty("toplam_pay").GetString());
        Assert.Equal("1/3", cocuklar.GetProperty("kisi_basi_pay").GetString());
        Assert.Equal("1/2", payload.GetProperty("tasarruf_edilebilir_kisim").GetString());
    }

    [Fact]
    public async Task Hic_mirasci_yoksa_devlet()
    {
        var payload = Payload(await new MirasPayTool().InvokeAsync(Args(
            """{ "es_var": false, "cocuk_sayisi": 0, "anne_var": false, "baba_var": false }"""), Context));

        Assert.Contains("Devlet", payload.GetProperty("aciklama").GetString(), StringComparison.Ordinal);
    }
}

using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class InfazToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Adi_suc_yarim_oran_normal_ds()
    {
        // Suç 2024 (31.07.2023 sonrası, geçici m.10 yok): 6 yıl, oran 1/2, DS 1 yıl.
        var result = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2024-01-01", "ceza_turu": "sureli", "ceza_yil": 6, "suc_tipi": "adi", "infaz_baslangic": "2026-01-01" }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal("1/2", payload.GetProperty("kosullu_saliverilme_orani").GetString());
        Assert.Equal("2029-01-01", payload.GetProperty("kosullu_saliverilme_tarihi").GetString());
        Assert.Equal(1, payload.GetProperty("denetimli_serbestlik_suresi_yil").GetInt32());
        Assert.Equal("2028-01-01", payload.GetProperty("denetimli_serbestlik_tarihi").GetString());
        Assert.Equal("2032-01-01", payload.GetProperty("bihakkin_tahliye_tarihi").GetString());
        Assert.Equal(2191, payload.GetProperty("toplam_ceza_gun").GetInt32());
    }

    [Fact]
    public async Task Mahsup_tum_tarihlerden_dusulur()
    {
        var result = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2024-01-01", "ceza_turu": "sureli", "ceza_yil": 6, "suc_tipi": "adi", "infaz_baslangic": "2026-01-01", "mahsup_gun": 100 }"""), Context);

        var payload = Payload(result);
        Assert.Equal("2028-09-23", payload.GetProperty("kosullu_saliverilme_tarihi").GetString());
        Assert.Equal("2031-09-23", payload.GetProperty("bihakkin_tahliye_tarihi").GetString());
    }

    [Fact]
    public async Task Tekerrur_orani_suc_tipi_oranindan_dusuk_olamaz()
    {
        // Adi suçta 1. mükerrir: 1/2 yerine 2/3.
        var birinci = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2024-01-01", "ceza_turu": "sureli", "ceza_yil": 6, "suc_tipi": "adi", "tekerrur": 1, "infaz_baslangic": "2026-01-01" }"""), Context);
        Assert.Equal("2/3", Payload(birinci).GetProperty("kosullu_saliverilme_orani").GetString());

        // Cinsel suçta 1. mükerrir: 3/4 > 2/3 olduğundan 3/4 kalır.
        var cinsel = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2024-01-01", "ceza_turu": "sureli", "ceza_yil": 6, "suc_tipi": "cinsel", "tekerrur": 1, "infaz_baslangic": "2026-01-01" }"""), Context);
        Assert.Equal("3/4", Payload(cinsel).GetProperty("kosullu_saliverilme_orani").GetString());

        // 2. mükerrir: 3/4.
        var ikinci = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2024-01-01", "ceza_turu": "sureli", "ceza_yil": 6, "suc_tipi": "adi", "tekerrur": 2, "infaz_baslangic": "2026-01-01" }"""), Context);
        Assert.Equal("3/4", Payload(ikinci).GetProperty("kosullu_saliverilme_orani").GetString());
    }

    [Fact]
    public async Task Gecici_m10_covid_oncesi_suc_uc_yil_ds()
    {
        // Suç 30.03.2020'den önce: basitleştirilmiş geçici m.10 ile DS 3 yıl.
        var result = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2019-01-01", "ceza_turu": "sureli", "ceza_yil": 10, "suc_tipi": "adi", "infaz_baslangic": "2026-01-01" }"""), Context);

        var payload = Payload(result);
        Assert.Equal(3, payload.GetProperty("denetimli_serbestlik_suresi_yil").GetInt32());
        Assert.Equal("2031-01-01", payload.GetProperty("kosullu_saliverilme_tarihi").GetString());
        Assert.Equal("2028-01-01", payload.GetProperty("denetimli_serbestlik_tarihi").GetString());
        Assert.Contains(payload.GetProperty("uyarilar").EnumerateArray(), u => u.GetString()!.Contains("geçici m.10", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Gecici_m10_covid_donemi_suc_iki_yil_ds()
    {
        var result = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2022-01-01", "ceza_turu": "sureli", "ceza_yil": 10, "suc_tipi": "adi", "infaz_baslangic": "2026-01-01" }"""), Context);

        Assert.Equal(2, Payload(result).GetProperty("denetimli_serbestlik_suresi_yil").GetInt32());
    }

    [Fact]
    public async Task Teror_gecici_m10_disi_ve_dortte_uc_oran()
    {
        var result = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2022-01-01", "ceza_turu": "sureli", "ceza_yil": 8, "suc_tipi": "teror_orgutlu", "infaz_baslangic": "2026-01-01" }"""), Context);

        var payload = Payload(result);
        Assert.Equal("3/4", payload.GetProperty("kosullu_saliverilme_orani").GetString());
        Assert.Equal(1, payload.GetProperty("denetimli_serbestlik_suresi_yil").GetInt32());
    }

    [Fact]
    public async Task Muebbet_24_yil_bihakkin_yok()
    {
        var result = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2024-05-01", "ceza_turu": "muebbet", "suc_tipi": "kasten_oldurme", "infaz_baslangic": "2026-01-01" }"""), Context);

        var payload = Payload(result);
        Assert.Equal("24 yıl", payload.GetProperty("kosullu_saliverilme_orani").GetString());
        Assert.Equal("2050-01-01", payload.GetProperty("kosullu_saliverilme_tarihi").GetString());
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("bihakkin_tahliye_tarihi").ValueKind);
    }

    [Fact]
    public async Task Ozel_durum_cocuklu_kadin_iki_yil_ds()
    {
        var result = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2024-01-01", "ceza_turu": "sureli", "ceza_yil": 6, "suc_tipi": "adi", "infaz_baslangic": "2026-01-01", "ozel_durum": "cocuklu_kadin" }"""), Context);

        Assert.Equal(2, Payload(result).GetProperty("denetimli_serbestlik_suresi_yil").GetInt32());
    }

    [Fact]
    public async Task Suc_tarihinde_18_yas_alti_uyari_verir()
    {
        var result = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2024-01-01", "dogum_tarihi": "2008-06-01", "ceza_turu": "sureli", "ceza_yil": 4, "suc_tipi": "adi", "infaz_baslangic": "2026-01-01" }"""), Context);

        Assert.Contains(Payload(result).GetProperty("uyarilar").EnumerateArray(), u => u.GetString()!.Contains("18 yaş", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sureli_cezada_ceza_girilmezse_hata()
    {
        var result = await new InfazTool().InvokeAsync(Args(
            """{ "suc_tarihi": "2024-01-01", "ceza_turu": "sureli", "suc_tipi": "adi", "infaz_baslangic": "2026-01-01" }"""), Context);

        Assert.True(result.IsError);
    }
}

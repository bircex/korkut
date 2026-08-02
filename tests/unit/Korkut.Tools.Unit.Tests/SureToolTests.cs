using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class SureToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    private static async Task<JsonElement> SonGunAsync(string sureTuru, string teblig)
    {
        var result = await new SureTool().InvokeAsync(Args(
            $$"""{ "sure_turu": "{{sureTuru}}", "teblig_tarihi": "{{teblig}}" }"""), Context);
        Assert.False(result.IsError);
        return Payload(result);
    }

    [Fact]
    public async Task Hukuk_istinafi_iki_hafta()
    {
        // Pazartesi tebliğ: 2 hafta sonra aynı gün (HMK m.92), iş günü.
        var payload = await SonGunAsync("istinaf_hukuk", "2026-01-05");
        Assert.Equal("2026-01-19", payload.GetProperty("son_gun").GetString());
    }

    [Fact]
    public async Task Hafta_sonuna_denk_gelen_son_gun_pazartesiye_uzar()
    {
        // 03.01.2026 + 7 gün = 10.01.2026 Cumartesi; ilk iş günü 12.01 Pazartesi.
        var payload = await SonGunAsync("istinaf_ceza", "2026-01-03");
        Assert.Equal("2026-01-12", payload.GetProperty("son_gun").GetString());
        Assert.Contains(payload.GetProperty("kurallar").EnumerateArray(), k => k.GetString()!.Contains("hafta sonu", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Resmi_tatile_denk_gelen_son_gun_ilk_is_gunune_uzar()
    {
        // 08.04.2026 + 15 gün = 23.04.2026 (tatil, Perşembe); 24.04 Cuma.
        var payload = await SonGunAsync("temyiz_ceza", "2026-04-08");
        Assert.Equal("2026-04-24", payload.GetProperty("son_gun").GetString());
    }

    [Fact]
    public async Task Kurban_bayrami_zinciri_ilk_is_gunune_kadar_uzar()
    {
        // 20.05.2026 + 7 gün = 27.05.2026 Kurban 1. gün; 28-30 bayram, 31 Pazar, son gün 01.06 Pazartesi.
        var payload = await SonGunAsync("icra_itiraz", "2026-05-20");
        Assert.Equal("2026-06-01", payload.GetProperty("son_gun").GetString());
    }

    [Fact]
    public async Task Hukuk_temyizinde_adli_tatil_uzamasi()
    {
        // 20.07.2026 + 2 hafta = 03.08.2026 adli tatil içinde; HMK m.104 ile 7 Eylül.
        var payload = await SonGunAsync("temyiz_hukuk", "2026-07-20");
        Assert.Equal("2026-09-07", payload.GetProperty("son_gun").GetString());
        Assert.Contains(payload.GetProperty("kurallar").EnumerateArray(), k => k.GetString()!.Contains("adli tatil", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ceza_temyizi_adli_tatilden_etkilenmez()
    {
        // 20.07.2026 + 15 gün = 04.08.2026 Salı; ceza süresi adli tatilde de işler.
        var payload = await SonGunAsync("temyiz_ceza", "2026-07-20");
        Assert.Equal("2026-08-04", payload.GetProperty("son_gun").GetString());
    }

    [Fact]
    public async Task Idari_yargida_calismaya_ara_verme_uzamasi()
    {
        // 01.07.2026 + 30 gün = 31.07.2026 ara verme içinde; İYUK m.8/3 ile 7 Eylül.
        var payload = await SonGunAsync("istinaf_idare", "2026-07-01");
        Assert.Equal("2026-09-07", payload.GetProperty("son_gun").GetString());
    }

    [Fact]
    public async Task Itirazin_iptali_bir_yil_ve_bilinmeyen_yil_uyarisi()
    {
        var payload = await SonGunAsync("itirazin_iptali", "2026-02-10");
        Assert.Equal("2027-02-10", payload.GetProperty("son_gun").GetString());
        Assert.Contains(payload.GetProperty("uyarilar").EnumerateArray(), u => u.GetString()!.Contains("2027", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var result = await new SureTool().InvokeAsync(Args(
            """{ "sure_turu": "istinaf_hukuk" }"""), Context);

        Assert.True(result.IsError);
    }
}

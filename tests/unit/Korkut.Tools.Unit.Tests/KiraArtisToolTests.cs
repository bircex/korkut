using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class KiraArtisToolTests
{
    private static readonly AgentToolContext Ctx = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Yenileme_ayi_bir_onceki_ayin_endeksiyle_hesaplanir()
    {
        // Temmuz 2026 yenilemesi Haziran 2026 endeksini kullanır: %32,03.
        var result = await new KiraArtisTool().InvokeAsync(Args(
            """{ "mevcut_kira": 10000, "yenileme_ayi": "2026-07" }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal("2026-06", payload.GetProperty("endeks_ayi").GetString());
        Assert.Equal(32.03m, payload.GetProperty("uygulanan_oran").GetDecimal());
        Assert.Equal(3203.00m, payload.GetProperty("azami_artis_tutari").GetDecimal());
        Assert.Equal(13203.00m, payload.GetProperty("azami_yeni_kira").GetDecimal());
        Assert.Contains("işyeri", payload.GetProperty("not").GetString());
        Assert.Contains("TBK m.344", payload.GetProperty("kaynak").GetString());
    }

    [Fact]
    public async Task Kurus_iceren_kira_iki_haneye_yuvarlanir()
    {
        // Ocak 2026 yenilemesi Aralık 2025 endeksi: %34,88; 7543,21 x 0,3488 = 2631,07.
        var result = await new KiraArtisTool().InvokeAsync(Args(
            """{ "mevcut_kira": 7543.21, "yenileme_ayi": "2026-01" }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(2631.07m, payload.GetProperty("azami_artis_tutari").GetDecimal());
        Assert.Equal(10174.28m, payload.GetProperty("azami_yeni_kira").GetDecimal());
    }

    [Fact]
    public async Task Tabloda_olmayan_ay_icin_oran_istenir()
    {
        var result = await new KiraArtisTool().InvokeAsync(Args(
            """{ "mevcut_kira": 10000, "yenileme_ayi": "2030-01" }"""), Ctx);

        Assert.True(result.IsError);
        Assert.Contains("tufe_orani", result.Content);
        Assert.Contains("2029-12", result.Content); // istenen endeks ayı açıkça söylenir
    }

    [Fact]
    public async Task Verilen_tufe_orani_tabloya_tercih_edilir()
    {
        var result = await new KiraArtisTool().InvokeAsync(Args(
            """{ "mevcut_kira": 10000, "yenileme_ayi": "2030-01", "tufe_orani": 25 }"""), Ctx);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(25m, payload.GetProperty("uygulanan_oran").GetDecimal());
        Assert.Equal(12500.00m, payload.GetProperty("azami_yeni_kira").GetDecimal());
        Assert.Contains("Kullanıcının verdiği", payload.GetProperty("kaynak").GetString());
    }

    [Fact]
    public async Task Bicimsiz_yenileme_ayi_hata_doner()
    {
        var result = await new KiraArtisTool().InvokeAsync(Args(
            """{ "mevcut_kira": 10000, "yenileme_ayi": "Temmuz 2026" }"""), Ctx);

        Assert.True(result.IsError);
        Assert.Contains("YYYY-MM", result.Content);
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var result = await new KiraArtisTool().InvokeAsync(Args(
            """{ "mevcut_kira": 0, "yenileme_ayi": "2026-07" }"""), Ctx);
        Assert.True(result.IsError);

        result = await new KiraArtisTool().InvokeAsync(Args(
            """{ "mevcut_kira": 10000, "yenileme_ayi": "2026-07", "tufe_orani": -3 }"""), Ctx);
        Assert.True(result.IsError);
    }
}

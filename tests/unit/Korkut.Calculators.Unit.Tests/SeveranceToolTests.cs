using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class SeveranceToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Alti_yil_kidem_yan_haklarla_hesaplanir()
    {
        // 01.01.2020 - 01.08.2026: 2404 gün = 6 tam yıl + 214 gün; giydirilmiş 53.000 < tavan 73.729,87.
        var result = await new SeveranceTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ise_giris": "2020-01-01", "isten_cikis": "2026-08-01", "brut_maas": 50000, "yemek": 2000, "yol": 1000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(6, payload.GetProperty("tam_yil").GetInt32());
        Assert.Equal(214, payload.GetProperty("kalan_gun").GetInt32());
        Assert.Equal(53000.00m, payload.GetProperty("giydirilmis_brut").GetDecimal());
        Assert.False(payload.GetProperty("tavan_uygulandi").GetBoolean());
        Assert.Equal(349073.97m, payload.GetProperty("brut_kidem_tazminati").GetDecimal());
        Assert.Equal(2649.47m, payload.GetProperty("damga_vergisi").GetDecimal());
        Assert.Equal(346424.50m, payload.GetProperty("net_kidem_tazminati").GetDecimal());
    }

    [Fact]
    public async Task Tavani_asan_ucret_tavandan_hesaplanir()
    {
        // Çıkış 01.03.2026: 2026 ilk yarı tavanı 64.948,77; giydirilmiş 100.000 tavana iner.
        var result = await new SeveranceTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ise_giris": "2016-03-01", "isten_cikis": "2026-03-01", "brut_maas": 100000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.True(payload.GetProperty("tavan_uygulandi").GetBoolean());
        Assert.Equal(64948.77m, payload.GetProperty("tavan").GetDecimal());
        Assert.Equal(64948.77m, payload.GetProperty("esas_ucret").GetDecimal());
        Assert.Equal(649843.58m, payload.GetProperty("brut_kidem_tazminati").GetDecimal());
    }

    [Fact]
    public async Task Temmuz_2026_sonrasi_cikista_yeni_tavan_secilir()
    {
        var result = await new SeveranceTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ise_giris": "2020-01-01", "isten_cikis": "2026-07-15", "brut_maas": 90000 }"""), Context);

        Assert.False(result.IsError);
        Assert.Equal(73729.87m, Payload(result).GetProperty("tavan").GetDecimal());
    }

    [Fact]
    public async Task Bir_yildan_az_kidem_hata_doner()
    {
        var result = await new SeveranceTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ise_giris": "2026-01-01", "isten_cikis": "2026-06-01", "brut_maas": 50000 }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Tavan_tablosu_oncesi_cikis_hata_doner()
    {
        var result = await new SeveranceTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ise_giris": "2020-01-01", "isten_cikis": "2023-06-01", "brut_maas": 50000 }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var result = await new SeveranceTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ise_giris": "2020-01-01", "isten_cikis": "2026-08-01", "brut_maas": -1 }"""), Context);

        Assert.True(result.IsError);
    }
}

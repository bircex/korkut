using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class NoticePayToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Yirmi_ay_kidem_alti_hafta_ihbar()
    {
        // Günlük 2.000; 6 hafta x 7 gün x 2.000 = 84.000 brüt; GV %15 = 12.600; damga 637,56.
        var result = await new NoticePayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "kidem_ay": 20, "brut_maas": 60000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(6, payload.GetProperty("ihbar_suresi_hafta").GetInt32());
        Assert.Equal(84000.00m, payload.GetProperty("brut_ihbar_tazminati").GetDecimal());
        Assert.Equal(12600.00m, payload.GetProperty("gelir_vergisi_yaklasik").GetDecimal());
        Assert.Equal(637.56m, payload.GetProperty("damga_vergisi").GetDecimal());
        Assert.Equal(70762.44m, payload.GetProperty("net_ihbar_tazminati_yaklasik").GetDecimal());
    }

    [Theory]
    [InlineData(5, 2)]
    [InlineData(6, 4)]
    [InlineData(17.9, 4)]
    [InlineData(18, 6)]
    [InlineData(36, 6)]
    [InlineData(37, 8)]
    public async Task Ihbar_suresi_4857_m17_basamaklarina_uyar(double kidemAy, int beklenenHafta)
    {
        var result = await new NoticePayTool(TestCalc.Service).InvokeAsync(Args(
            $$"""{ "kidem_ay": {{kidemAy}}, "brut_maas": 30000 }"""), Context);

        Assert.False(result.IsError);
        Assert.Equal(beklenenHafta, Payload(result).GetProperty("ihbar_suresi_hafta").GetInt32());
    }

    [Fact]
    public async Task Tarih_ciftinden_kidem_hesaplanir()
    {
        // 577 gün / 30 = 19,23 ay -> 6 hafta.
        var result = await new NoticePayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "ise_giris": "2025-01-01", "isten_cikis": "2026-08-01", "brut_maas": 60000 }"""), Context);

        Assert.False(result.IsError);
        Assert.Equal(6, Payload(result).GetProperty("ihbar_suresi_hafta").GetInt32());
    }

    [Fact]
    public async Task Yan_haklar_giydirilmis_ucrete_dahil_edilir()
    {
        // Giydirilmiş 30.000, günlük 1.000; 6 hafta -> 42.000 brüt; GV 6.300; damga 318,78.
        var result = await new NoticePayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "kidem_ay": 20, "brut_maas": 27000, "yemek": 2000, "yol": 1000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(30000.00m, payload.GetProperty("giydirilmis_brut").GetDecimal());
        Assert.Equal(42000.00m, payload.GetProperty("brut_ihbar_tazminati").GetDecimal());
        Assert.Equal(35381.22m, payload.GetProperty("net_ihbar_tazminati_yaklasik").GetDecimal());
    }

    [Fact]
    public async Task Kidem_bilgisi_yoksa_hata_doner()
    {
        var result = await new NoticePayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "brut_maas": 60000 }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Gecersiz_ucret_hata_doner()
    {
        var result = await new NoticePayTool(TestCalc.Service).InvokeAsync(Args(
            """{ "kidem_ay": 12, "brut_maas": 0 }"""), Context);

        Assert.True(result.IsError);
    }
}

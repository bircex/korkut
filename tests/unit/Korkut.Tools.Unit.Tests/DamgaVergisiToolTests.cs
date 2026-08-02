using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class DamgaVergisiToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Sozlesme_binde_dokuz_kirk_sekiz()
    {
        var result = await new DamgaVergisiTool().InvokeAsync(Args(
            """{ "kagit_turu": "sozlesme", "tutar": 1000000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(9480.00m, payload.GetProperty("vergi").GetDecimal());
        Assert.False(payload.GetProperty("azami_uygulandi").GetBoolean());
    }

    [Fact]
    public async Task Kira_sozlesmesi_binde_bir_seksen_dokuz()
    {
        var result = await new DamgaVergisiTool().InvokeAsync(Args(
            """{ "kagit_turu": "kira_sozlesmesi", "tutar": 100000 }"""), Context);

        Assert.Equal(189.00m, Payload(result).GetProperty("vergi").GetDecimal());
    }

    [Fact]
    public async Task Ihale_karari_ve_ucret_odemesi_oranlari()
    {
        var ihale = await new DamgaVergisiTool().InvokeAsync(Args(
            """{ "kagit_turu": "ihale_karari", "tutar": 1000000 }"""), Context);
        var ucret = await new DamgaVergisiTool().InvokeAsync(Args(
            """{ "kagit_turu": "ucret_odemesi", "tutar": 100000 }"""), Context);

        Assert.Equal(5690.00m, Payload(ihale).GetProperty("vergi").GetDecimal());
        Assert.Equal(759.00m, Payload(ucret).GetProperty("vergi").GetDecimal());
    }

    [Fact]
    public async Task Azami_damga_vergisi_siniri_uygulanir()
    {
        // 5 milyar x binde 9,48 = 47,4 milyon > 2026 azami 29.115.961,10
        var result = await new DamgaVergisiTool().InvokeAsync(Args(
            """{ "kagit_turu": "sozlesme", "tutar": 5000000000 }"""), Context);

        var payload = Payload(result);
        Assert.True(payload.GetProperty("azami_uygulandi").GetBoolean());
        Assert.Equal(29115961.10m, payload.GetProperty("vergi").GetDecimal());
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var tur = await new DamgaVergisiTool().InvokeAsync(Args(
            """{ "kagit_turu": "senet", "tutar": 1000 }"""), Context);
        var tutar = await new DamgaVergisiTool().InvokeAsync(Args(
            """{ "kagit_turu": "sozlesme", "tutar": -1 }"""), Context);

        Assert.True(tur.IsError);
        Assert.True(tutar.IsError);
    }
}

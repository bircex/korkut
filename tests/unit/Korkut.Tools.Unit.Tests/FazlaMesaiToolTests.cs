using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class FazlaMesaiToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Kirilimli_hesap_dogru()
    {
        // Saatlik 45.000/225 = 200; günlük 1.500.
        // FM: 10 x 200 x 1,5 = 3.000; HT: 8 x 200 x 2,5 = 4.000; RT: 2 x 1.500 x 2 = 6.000.
        var result = await new FazlaMesaiTool().InvokeAsync(Args(
            """{ "aylik_brut_ucret": 45000, "fazla_mesai_saat": 10, "hafta_tatili_saat": 8, "resmi_tatil_gun": 2 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(200.00m, payload.GetProperty("saatlik_ucret").GetDecimal());
        Assert.Equal(1500.00m, payload.GetProperty("gunluk_ucret").GetDecimal());
        Assert.Equal(3000.00m, payload.GetProperty("fazla_mesai_brut").GetDecimal());
        Assert.Equal(4000.00m, payload.GetProperty("hafta_tatili_brut").GetDecimal());
        Assert.Equal(6000.00m, payload.GetProperty("resmi_tatil_brut").GetDecimal());
        Assert.Equal(13000.00m, payload.GetProperty("toplam_brut").GetDecimal());
    }

    [Fact]
    public async Task Verilmeyen_kalemler_sifir_sayilir()
    {
        var result = await new FazlaMesaiTool().InvokeAsync(Args(
            """{ "aylik_brut_ucret": 45000, "fazla_mesai_saat": 4 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(1200.00m, payload.GetProperty("fazla_mesai_brut").GetDecimal());
        Assert.Equal(0.00m, payload.GetProperty("hafta_tatili_brut").GetDecimal());
        Assert.Equal(0.00m, payload.GetProperty("resmi_tatil_brut").GetDecimal());
        Assert.Equal(1200.00m, payload.GetProperty("toplam_brut").GetDecimal());
    }

    [Fact]
    public async Task Negatif_saat_hata_doner()
    {
        var result = await new FazlaMesaiTool().InvokeAsync(Args(
            """{ "aylik_brut_ucret": 45000, "fazla_mesai_saat": -3 }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Ucret_olmadan_hata_doner()
    {
        var result = await new FazlaMesaiTool().InvokeAsync(Args(
            """{ "fazla_mesai_saat": 10 }"""), Context);

        Assert.True(result.IsError);
    }
}

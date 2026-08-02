using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class VekaletUcretiToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Nispi_iki_dilime_yayilan_dava_degeri()
    {
        // 600.000 x %16 + 400.000 x %15 = 96.000 + 60.000 = 156.000
        var result = await new VekaletUcretiTool().InvokeAsync(Args(
            """{ "kategori": "para_davasi", "dava_degeri": 1000000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(156000.00m, payload.GetProperty("ucret").GetDecimal());
        Assert.Equal(2, payload.GetProperty("dilim_kirilimi").GetArrayLength());
        Assert.False(payload.GetProperty("taban_uygulandi").GetBoolean());
    }

    [Fact]
    public async Task Nispi_ilk_dilim_siniri_tek_dilimde_kalir()
    {
        var result = await new VekaletUcretiTool().InvokeAsync(Args(
            """{ "kategori": "para_davasi", "dava_degeri": 600000 }"""), Context);

        var payload = Payload(result);
        Assert.Equal(96000.00m, payload.GetProperty("ucret").GetDecimal());
        Assert.Equal(1, payload.GetProperty("dilim_kirilimi").GetArrayLength());
    }

    [Fact]
    public async Task Nispi_tum_dilimleri_asan_deger()
    {
        // 18.600.000 TL'lik dilimler + 1.400.000 x %1 = 1.256.000
        var result = await new VekaletUcretiTool().InvokeAsync(Args(
            """{ "kategori": "para_davasi", "dava_degeri": 20000000 }"""), Context);

        var payload = Payload(result);
        Assert.Equal(1256000.00m, payload.GetProperty("ucret").GetDecimal());
        Assert.Equal(10, payload.GetProperty("dilim_kirilimi").GetArrayLength());
    }

    [Fact]
    public async Task Nispi_dusuk_degerde_asliye_maktu_tabani_uygulanir()
    {
        // 100.000 x %16 = 16.000 < asliye maktu 45.000
        var result = await new VekaletUcretiTool().InvokeAsync(Args(
            """{ "kategori": "para_davasi", "dava_degeri": 100000 }"""), Context);

        var payload = Payload(result);
        Assert.True(payload.GetProperty("taban_uygulandi").GetBoolean());
        Assert.Equal(45000.00m, payload.GetProperty("ucret").GetDecimal());
        Assert.Equal(16000.00m, payload.GetProperty("nispi_ucret").GetDecimal());
    }

    [Fact]
    public async Task Taban_kategori_parametresi_tabani_degistirir()
    {
        var result = await new VekaletUcretiTool().InvokeAsync(Args(
            """{ "kategori": "para_davasi", "dava_degeri": 100000, "taban_kategori": "tuketici" }"""), Context);

        Assert.Equal(22500.00m, Payload(result).GetProperty("ucret").GetDecimal());
    }

    [Fact]
    public async Task Maktu_kategori_dogrudan_tarife_degerini_doner()
    {
        var result = await new VekaletUcretiTool().InvokeAsync(Args(
            """{ "kategori": "agir_ceza" }"""), Context);

        var payload = Payload(result);
        Assert.Equal(65000.00m, payload.GetProperty("ucret").GetDecimal());
        Assert.Contains("33067", payload.GetProperty("kaynak").GetString());
    }

    [Fact]
    public async Task Para_davasi_dava_degeri_olmadan_hata_doner()
    {
        var result = await new VekaletUcretiTool().InvokeAsync(Args(
            """{ "kategori": "para_davasi" }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Bilinmeyen_kategori_hata_doner()
    {
        var result = await new VekaletUcretiTool().InvokeAsync(Args(
            """{ "kategori": "uzay_mahkemesi" }"""), Context);

        Assert.True(result.IsError);
    }
}

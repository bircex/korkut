using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class GrossNetSalaryToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Asgari_ucret_brutunden_resmi_neti_verir()
    {
        var result = await new GrossNetSalaryTool().InvokeAsync(Args(
            """{ "yon": "brutten_nete", "tutar": 33030 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(28075.50m, payload.GetProperty("net_aylik").GetDecimal());
        var kesintiler = payload.GetProperty("kesintiler");
        Assert.Equal(4624.20m, kesintiler.GetProperty("sgk_isci").GetDecimal());
        Assert.Equal(330.30m, kesintiler.GetProperty("issizlik_isci").GetDecimal());
        Assert.Equal(0.00m, kesintiler.GetProperty("gelir_vergisi").GetDecimal());
        Assert.Equal(0.00m, kesintiler.GetProperty("damga_vergisi").GetDecimal());
    }

    [Fact]
    public async Task Yuz_bin_brut_ilk_ay()
    {
        // SGK 14.000 + işsizlik 1.000; matrah 85.000; GV 12.750 - istisna 4.211,325 = 8.538,68;
        // damga (100.000 - 33.030) x 0,00759 = 508,30; net 75.953,02.
        var result = await new GrossNetSalaryTool().InvokeAsync(Args(
            """{ "yon": "brutten_nete", "tutar": 100000, "ay": 1 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        var kesintiler = payload.GetProperty("kesintiler");
        Assert.Equal(14000.00m, kesintiler.GetProperty("sgk_isci").GetDecimal());
        Assert.Equal(8538.68m, kesintiler.GetProperty("gelir_vergisi").GetDecimal());
        Assert.Equal(508.30m, kesintiler.GetProperty("damga_vergisi").GetDecimal());
        Assert.Equal(75953.02m, payload.GetProperty("net_aylik").GetDecimal());
    }

    [Fact]
    public async Task Aralik_ayinda_kumulatif_dilim_uygulanir()
    {
        // 12. ayda kümülatif matrah 1.020.000 -> %27 dilimi; GV 17.334,90; net 67.156,80.
        var result = await new GrossNetSalaryTool().InvokeAsync(Args(
            """{ "yon": "brutten_nete", "tutar": 100000, "ay": 12 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(17334.90m, payload.GetProperty("kesintiler").GetProperty("gelir_vergisi").GetDecimal());
        Assert.Equal(67156.80m, payload.GetProperty("net_aylik").GetDecimal());
    }

    [Fact]
    public async Task Sgk_tavani_uygulanir()
    {
        // Brüt 300.000 > tavan 247.725: SGK 34.681,50 + işsizlik 2.477,25; net 221.958,03.
        var result = await new GrossNetSalaryTool().InvokeAsync(Args(
            """{ "yon": "brutten_nete", "tutar": 300000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        var kesintiler = payload.GetProperty("kesintiler");
        Assert.Equal(34681.50m, kesintiler.GetProperty("sgk_isci").GetDecimal());
        Assert.Equal(2477.25m, kesintiler.GetProperty("issizlik_isci").GetDecimal());
        Assert.Equal(221958.03m, payload.GetProperty("net_aylik").GetDecimal());
    }

    [Fact]
    public async Task Yillik_projeksiyon_kumulatif_neti_toplar()
    {
        var result = await new GrossNetSalaryTool().InvokeAsync(Args(
            """{ "yon": "brutten_nete", "tutar": 100000 }"""), Context);

        Assert.False(result.IsError);
        var projeksiyon = Payload(result).GetProperty("yillik_projeksiyon");
        Assert.Equal(1200000.00m, projeksiyon.GetProperty("yillik_brut").GetDecimal());
        Assert.Equal(833881.57m, projeksiyon.GetProperty("yillik_net").GetDecimal());
    }

    [Fact]
    public async Task Netten_brute_asgari_ucreti_bulur()
    {
        var result = await new GrossNetSalaryTool().InvokeAsync(Args(
            """{ "yon": "netten_brute", "tutar": 28075.50 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        var brut = payload.GetProperty("brut_aylik").GetDecimal();
        Assert.True(Math.Abs(brut - 33030.00m) <= 0.05m, $"brut={brut}");
        Assert.Equal(28075.50m, payload.GetProperty("net_aylik").GetDecimal());
    }

    [Fact]
    public async Task Netten_brute_yuvarlak_donus()
    {
        var result = await new GrossNetSalaryTool().InvokeAsync(Args(
            """{ "yon": "netten_brute", "tutar": 75953.02, "ay": 1 }"""), Context);

        Assert.False(result.IsError);
        var brut = Payload(result).GetProperty("brut_aylik").GetDecimal();
        Assert.True(Math.Abs(brut - 100000.00m) <= 0.05m, $"brut={brut}");
    }

    [Fact]
    public async Task Gecersiz_yon_hata_doner()
    {
        var result = await new GrossNetSalaryTool().InvokeAsync(Args(
            """{ "yon": "tersine", "tutar": 100 }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Gecersiz_ay_hata_doner()
    {
        var result = await new GrossNetSalaryTool().InvokeAsync(Args(
            """{ "yon": "brutten_nete", "tutar": 100000, "ay": 13 }"""), Context);

        Assert.True(result.IsError);
    }
}

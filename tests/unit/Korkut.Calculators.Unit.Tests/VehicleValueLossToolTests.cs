using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Calculators.Unit.Tests;

public class VehicleValueLossToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Resmi_formul_buyuk_hasar()
    {
        // Baz: 1.000.000 x %19 = 190.000; büyük hasar 0,90; 20.000 km 0,80 -> 136.800.
        var result = await new VehicleValueLossTool(TestCalc.Service).InvokeAsync(Args(
            """{ "piyasa_degeri": 1000000, "kilometre": 20000, "hasar_boyutu": "buyuk" }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(190_000.00m, payload.GetProperty("baz_deger_kaybi").GetDecimal());
        Assert.Equal(0.90m, payload.GetProperty("hasar_boyutu_katsayisi").GetDecimal());
        Assert.Equal(0.80m, payload.GetProperty("kilometre_katsayisi").GetDecimal());
        Assert.Equal(136_800.00m, payload.GetProperty("deger_kaybi").GetDecimal());
    }

    [Fact]
    public async Task Hasar_tutarindan_boyut_turetilir()
    {
        // 40.000 / 1.000.000 = %4 -> basit (0,25); 160.000 km -> 0,10; 190.000 x 0,25 x 0,10 = 4.750.
        var result = await new VehicleValueLossTool(TestCalc.Service).InvokeAsync(Args(
            """{ "piyasa_degeri": 1000000, "kilometre": 160000, "hasar_tutari": 40000 }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal("basit", payload.GetProperty("hasar_boyutu").GetString());
        Assert.Equal(0.10m, payload.GetProperty("kilometre_katsayisi").GetDecimal());
        Assert.Equal(4_750.00m, payload.GetProperty("deger_kaybi").GetDecimal());
        Assert.Contains(payload.GetProperty("varsayimlar").EnumerateArray().Select(v => v.GetString()),
            v => v!.Contains("pratik yaklaşım", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0, 0.90)]
    [InlineData(14999, 0.90)]
    [InlineData(15000, 0.80)]
    [InlineData(30000, 0.60)]
    [InlineData(45000, 0.40)]
    [InlineData(60000, 0.30)]
    [InlineData(75000, 0.20)]
    [InlineData(149999, 0.20)]
    [InlineData(150000, 0.10)]
    public async Task Kilometre_katsayisi_basamaklari(int kilometre, decimal katsayi)
    {
        var result = await new VehicleValueLossTool(TestCalc.Service).InvokeAsync(Args(
            $$"""{ "piyasa_degeri": 1000000, "kilometre": {{kilometre}}, "hasar_boyutu": "orta" }"""), Context);

        Assert.False(result.IsError);
        Assert.Equal(katsayi, Payload(result).GetProperty("kilometre_katsayisi").GetDecimal());
    }

    [Fact]
    public async Task Hasar_bilgisi_yoksa_hata_doner()
    {
        var result = await new VehicleValueLossTool(TestCalc.Service).InvokeAsync(Args(
            """{ "piyasa_degeri": 1000000, "kilometre": 20000 }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Bilirkisi_sapma_uyarisi_verilir()
    {
        var result = await new VehicleValueLossTool(TestCalc.Service).InvokeAsync(Args(
            """{ "piyasa_degeri": 500000, "kilometre": 10000, "hasar_boyutu": "kucuk" }"""), Context);

        Assert.False(result.IsError);
        Assert.Contains("bilirkişi", Payload(result).GetProperty("uyari").GetString(), StringComparison.Ordinal);
    }
}

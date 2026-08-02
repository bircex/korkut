using System.Text.Json;
using Korkut.Tools.Calc;
using Korkut.Tools.Core;

namespace Korkut.Tools.Unit.Tests;

public class InterestToolTests
{
    private static readonly AgentToolContext Context = new(Guid.NewGuid(), Guid.NewGuid(), new HashSet<string>());

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    private static JsonElement Payload(ToolResult result) => JsonDocument.Parse(result.Content).RootElement;

    [Fact]
    public async Task Ozel_oran_tam_yil_basit_faiz()
    {
        var result = await new InterestTool().InvokeAsync(Args(
            """{ "faiz_turu": "ozel", "ozel_oran": 10, "ana_para": 100000, "baslangic": "2025-01-01", "bitis": "2026-01-01" }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(365, payload.GetProperty("toplam_gun").GetInt32());
        Assert.Equal(10000.00m, payload.GetProperty("toplam_faiz").GetDecimal());
        Assert.Equal(110000.00m, payload.GetProperty("ana_para_arti_faiz").GetDecimal());
    }

    [Fact]
    public async Task Yasal_faiz_donem_gecisinde_parcali_hesaplanir()
    {
        // 2024: 152 gün %9 (01.01-01.06) + 214 gün %24 (01.06-01.01.2025).
        var result = await new InterestTool().InvokeAsync(Args(
            """{ "faiz_turu": "yasal", "ana_para": 100000, "baslangic": "2024-01-01", "bitis": "2025-01-01" }"""), Context);

        Assert.False(result.IsError);
        var payload = Payload(result);
        Assert.Equal(366, payload.GetProperty("toplam_gun").GetInt32());
        Assert.Equal(17819.18m, payload.GetProperty("toplam_faiz").GetDecimal());
        Assert.Equal(2, payload.GetProperty("donem_kirilimi").GetArrayLength());
    }

    [Fact]
    public async Task Yasal_faiz_31_07_2026_sonrasi_yuzde_31()
    {
        var result = await new InterestTool().InvokeAsync(Args(
            """{ "faiz_turu": "yasal", "ana_para": 365000, "baslangic": "2026-07-31", "bitis": "2026-08-01" }"""), Context);

        Assert.False(result.IsError);
        // 1 gün: 365000 * 0,31 / 365 = 310,00
        Assert.Equal(310.00m, Payload(result).GetProperty("toplam_faiz").GetDecimal());
    }

    [Fact]
    public async Task Bitis_dahil_bir_gun_ekler()
    {
        var result = await new InterestTool().InvokeAsync(Args(
            """{ "faiz_turu": "ozel", "ozel_oran": 10, "ana_para": 100000, "baslangic": "2025-01-01", "bitis": "2026-01-01", "bitis_dahil": true }"""), Context);

        Assert.Equal(366, Payload(result).GetProperty("toplam_gun").GetInt32());
    }

    [Fact]
    public async Task Avans_tablosu_oncesi_baslangic_hata_doner()
    {
        var result = await new InterestTool().InvokeAsync(Args(
            """{ "faiz_turu": "avans", "ana_para": 100000, "baslangic": "2022-01-01", "bitis": "2024-01-01" }"""), Context);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Gecersiz_girdi_hata_doner()
    {
        var result = await new InterestTool().InvokeAsync(Args(
            """{ "faiz_turu": "yasal", "ana_para": -5, "baslangic": "2025-01-01", "bitis": "2026-01-01" }"""), Context);

        Assert.True(result.IsError);
    }
}

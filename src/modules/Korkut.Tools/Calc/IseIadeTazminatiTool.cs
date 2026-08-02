using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>İşe iade davası parasal sonuçlarını hesaplar (4857 sK m.21: boşta geçen süre + işe başlatmama tazminatı).</summary>
public sealed class IseIadeTazminatiTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "aylik_brut": { "type": "number", "description": "Aylık brüt ücret, TL" },
            "kidem_yil": { "type": "number", "description": "Kıdem süresi, yıl olarak (örn. 3.5)" }
          },
          "required": ["aylik_brut", "kidem_yil"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_ise_iade_tazminati",
        "İşe iade davasının parasal sonuçlarını hesaplar: boşta geçen süre ücreti (en çok 4 aya kadar) ve işe başlatmama tazminatı (4-8 aylık ücret, kıdeme göre mahkeme takdiri; pratik bant verilir). Sonuçlar tahminidir, mahkeme takdirine tabidir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var aylikBrut = ToolArgs.Decimal(arguments, "aylik_brut");
        var kidemYil = ToolArgs.Decimal(arguments, "kidem_yil");
        if (aylikBrut is not { } aylik || aylik <= 0 || kidemYil is not { } kidem || kidem < 0)
        {
            return Task.FromResult(ToolResult.Error("Pozitif aylik_brut ve negatif olmayan kidem_yil zorunludur."));
        }

        // 4857 m.21/3: kararın kesinleşmesine kadar çalıştırılmayan süre için en çok 4 aya kadar ücret.
        const int BostaAy = 4;
        var bosta = decimal.Round(aylik * BostaAy, 2);

        // 4857 m.21/1: işe başlatmama tazminatı 4-8 aylık ücret; miktar mahkeme takdirinde.
        // Yerleşik pratik bant: 6 aydan az kıdemde 4 ay; 5 yıla kadar 4-5 ay; 15 yıla kadar 5-6 ay; üstü 6-8 ay.
        var (minAy, maxAy) = kidem switch
        {
            < 0.5m => (4, 4),
            < 5m => (4, 5),
            < 15m => (5, 6),
            _ => (6, 8),
        };

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            BostaGecenSureAy = BostaAy,
            BostaGecenSureUcreti = bosta,
            IseBaslatmamaAyAraligi = $"{minAy}-{maxAy} ay",
            IseBaslatmamaTazminatiMin = decimal.Round(aylik * minAy, 2),
            IseBaslatmamaTazminatiMax = decimal.Round(aylik * maxAy, 2),
            ToplamMin = decimal.Round(aylik * (BostaAy + minAy), 2),
            ToplamMax = decimal.Round(aylik * (BostaAy + maxAy), 2),
            Uyari = "İşe başlatmama tazminatının ay sayısı 4-8 aralığında mahkeme takdirindedir; verilen bant yerleşik uygulamadan çıkarılan pratik bir tahmindir. Boşta geçen süre ücreti giydirilmiş ücretten, işe başlatmama tazminatı çıplak brüt ücretten hesaplanır. İş güvencesi için en az 30 işçili işyeri, 6 ay kıdem ve belirsiz süreli sözleşme şartları aranır (4857 sK m.18).",
            Kaynak = "4857 sK m.18 (iş güvencesi kapsamı) ve m.21 (işe başlatmama tazminatı 4-8 ay, boşta geçen süre en çok 4 ay)",
        })));
    }
}

using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Yasal faiz, avans (ticari temerrüt) faizi veya özel oranla basit faiz hesaplar.</summary>
public sealed class FaizTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "faiz_turu": {
              "type": "string",
              "enum": ["yasal", "avans", "ozel"],
              "description": "yasal = 3095 m.1 kanuni faiz; avans = ticari işlerde temerrüt (TCMB avans oranı); ozel = ozel_oran ile sabit oran"
            },
            "ana_para": { "type": "number", "description": "TL" },
            "baslangic": { "type": "string", "format": "date", "description": "Faiz başlangıcı (temerrüt tarihi)" },
            "bitis": { "type": "string", "format": "date", "description": "Hesap sonu tarihi" },
            "bitis_dahil": { "type": "boolean", "description": "Bitiş günü de faize dahil edilsin mi (varsayılan hayır)" },
            "ozel_oran": { "type": "number", "description": "faiz_turu=ozel iken yıllık %" }
          },
          "required": ["faiz_turu", "ana_para", "baslangic", "bitis"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_faiz",
        "Yasal faiz, avans (ticari temerrüt) faizi veya özel oranla basit faiz hesaplar (ACT/365, dönem tablosuna göre parçalı). Oran dönemleri günceldir; 31.07.2026 sonrası yasal faiz %31'dir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var tur = ToolArgs.String(arguments, "faiz_turu");
        var anaPara = ToolArgs.Decimal(arguments, "ana_para");
        var baslangic = ToolArgs.Date(arguments, "baslangic");
        var bitis = ToolArgs.Date(arguments, "bitis");
        if (tur is null || anaPara is not { } p || p <= 0 || baslangic is not { } start || bitis is not { } end)
        {
            return Task.FromResult(ToolResult.Error("faiz_turu, pozitif ana_para, baslangic ve bitis zorunludur."));
        }

        var endExclusive = ToolArgs.Bool(arguments, "bitis_dahil") ? end.AddDays(1) : end;
        if (start >= endExclusive)
        {
            return Task.FromResult(ToolResult.Error("baslangic, bitis tarihinden önce olmalıdır."));
        }

        RatePeriod[] table;
        switch (tur)
        {
            case "yasal":
                table = LegalInterestRates.Yasal;
                break;
            case "avans":
                table = LegalInterestRates.Avans;
                break;
            case "ozel":
                if (ToolArgs.Decimal(arguments, "ozel_oran") is not { } oran || oran <= 0)
                {
                    return Task.FromResult(ToolResult.Error("faiz_turu=ozel için pozitif ozel_oran gerekir."));
                }

                table = [new RatePeriod(DateOnly.MinValue, null, oran)];
                break;
            default:
                return Task.FromResult(ToolResult.Error("faiz_turu yasal, avans veya ozel olmalıdır."));
        }

        if (!RateSchedule.Covers(table, start))
        {
            return Task.FromResult(ToolResult.Error(
                $"{tur} faiz tablosu {table[0].From:dd.MM.yyyy} öncesini kapsamıyor; daha eski dönem için ozel_oran ile hesaplayın."));
        }

        var segments = RateSchedule.Segments(table, start, endExclusive);
        decimal toplamFaiz = 0;
        var kirilim = new List<object>(segments.Count);
        foreach (var s in segments)
        {
            var faiz = p * (s.AnnualPercent / 100m) * s.Days / 365m;
            toplamFaiz += faiz;
            kirilim.Add(new
            {
                Baslangic = s.From,
                Bitis = s.To,
                YillikOran = s.AnnualPercent,
                Gun = s.Days,
                Faiz = decimal.Round(faiz, 2),
            });
        }

        toplamFaiz = decimal.Round(toplamFaiz, 2);
        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yontem = "Basit faiz, ACT/365, dönem tablosuna göre parçalı",
            ToplamGun = endExclusive.DayNumber - start.DayNumber,
            DonemKirilimi = kirilim,
            ToplamFaiz = toplamFaiz,
            AnaParaArtiFaiz = decimal.Round(p + toplamFaiz, 2),
            Kaynak = LegalInterestRates.Kaynak,
        })));
    }
}

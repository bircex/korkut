using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// 6183 sayılı AATUHK m.51 gecikme zammı hesaplar (vergi, SGK primi ve diğer amme alacakları).
/// Tam aylar aylık oranla, ay kesirleri günlük (aylık oran / 30) hesaplanır; oran değişikliğinde
/// her dönem kendi oranıyla ayrı hesaplanır.
/// </summary>
public sealed class LatePaymentSurchargeTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "ana_para": { "type": "number", "description": "Amme alacağının aslı, TL" },
            "vade_tarihi": { "type": "string", "format": "date", "description": "Borcun vade (son ödeme) tarihi" },
            "odeme_tarihi": { "type": "string", "format": "date", "description": "Fiili ödeme veya hesap tarihi" }
          },
          "required": ["ana_para", "vade_tarihi", "odeme_tarihi"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_gecikme_zammi",
        "6183 sayılı AATUHK m.51 gecikme zammı hesaplar (vergi, SGK ve diğer amme alacakları için; özel hukuk "
        + "alacaklarında calc_faiz kullanın). Aylık oran dönem tablosuna göre parçalı; ay kesirleri günlük hesaplanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var principal = ToolArgs.Decimal(arguments, "ana_para");
        var dueDate = ToolArgs.Date(arguments, "vade_tarihi");
        var paymentDate = ToolArgs.Date(arguments, "odeme_tarihi");
        if (principal is not { } principalValue || principalValue <= 0 || dueDate is not { } start || paymentDate is not { } end)
        {
            return Task.FromResult(ToolResult.Error("Pozitif ana_para, vade_tarihi ve odeme_tarihi zorunludur."));
        }

        if (start >= end)
        {
            return Task.FromResult(ToolResult.Error(
                "odeme_tarihi vade_tarihi'nden sonra olmalıdır; vadesinde veya öncesinde ödemede gecikme zammı doğmaz."));
        }

        var table = DelaySurchargeRates.Table;
        if (start < table[0].From)
        {
            return Task.FromResult(ToolResult.Error(
                $"Gecikme zammı oran tablosu {table[0].From:dd.MM.yyyy} öncesini kapsamıyor; daha eski vadeler için dönem oranlarını ayrıca doğrulayın."));
        }

        decimal totalSurcharge = 0;
        var totalMonths = 0;
        var totalFractionDays = 0;
        var breakdown = new List<object>();
        foreach (var period in table)
        {
            var from = period.From > start ? period.From : start;
            var to = period.To is { } t && t < end ? t : end;
            if (from >= to)
            {
                continue;
            }

            // Tam aylar: dönem başından ay ay ilerle (m.51 ay esası); artan süre gün kesridir.
            var anchor = from;
            var months = 0;
            while (anchor.AddMonths(1) <= to)
            {
                anchor = anchor.AddMonths(1);
                months++;
            }

            var days = to.DayNumber - anchor.DayNumber;
            var surcharge = principalValue * (period.MonthlyPercent / 100m) * (months + days / 30m);
            totalSurcharge += surcharge;
            totalMonths += months;
            totalFractionDays += days;
            breakdown.Add(new
            {
                Baslangic = from,
                Bitis = to,
                AylikOran = period.MonthlyPercent,
                TamAy = months,
                KesirGun = days,
                Zam = decimal.Round(surcharge, 2),
            });
        }

        totalSurcharge = decimal.Round(totalSurcharge, 2);
        string? note = null;
        if (totalSurcharge < 1m)
        {
            totalSurcharge = 1.00m; // m.51/2: gecikme zammı 1 TL'den az olamaz.
            note = "Hesaplanan zam 1 TL'nin altında kaldığından asgari 1 TL uygulandı (6183 m.51/2).";
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yontem = "6183 m.51: süre vade tarihinden ödeme tarihine kadardır; tam aylar aylık oranla, "
                + "ay kesirleri günlük (aylık oran / 30) hesaplanır, oran dönemleri ayrı ayrı uygulanır.",
            ToplamTamAy = totalMonths,
            ToplamKesirGun = totalFractionDays,
            DonemKirilimi = breakdown,
            ToplamZam = totalSurcharge,
            GenelToplam = decimal.Round(principalValue + totalSurcharge, 2),
            Not = note,
            Kaynak = DelaySurchargeRates.Kaynak,
        })));
    }
}

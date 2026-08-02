using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// 6183 sayılı AATUHK m.51 gecikme zammı hesaplar (vergi, SGK primi ve diğer amme alacakları).
/// Tam aylar aylık oranla, ay kesirleri günlük (aylık oran / 30) hesaplanır; oran değişikliğinde
/// her dönem kendi oranıyla ayrı hesaplanır.
/// </summary>
public sealed class GecikmeZammiTool : IAgentTool
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
        var anaPara = ToolArgs.Decimal(arguments, "ana_para");
        var vade = ToolArgs.Date(arguments, "vade_tarihi");
        var odeme = ToolArgs.Date(arguments, "odeme_tarihi");
        if (anaPara is not { } p || p <= 0 || vade is not { } start || odeme is not { } end)
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

        decimal toplamZam = 0;
        var toplamAy = 0;
        var toplamKesirGun = 0;
        var kirilim = new List<object>();
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
            var aylar = 0;
            while (anchor.AddMonths(1) <= to)
            {
                anchor = anchor.AddMonths(1);
                aylar++;
            }

            var gunler = to.DayNumber - anchor.DayNumber;
            var zam = p * (period.MonthlyPercent / 100m) * (aylar + gunler / 30m);
            toplamZam += zam;
            toplamAy += aylar;
            toplamKesirGun += gunler;
            kirilim.Add(new
            {
                Baslangic = from,
                Bitis = to,
                AylikOran = period.MonthlyPercent,
                TamAy = aylar,
                KesirGun = gunler,
                Zam = decimal.Round(zam, 2),
            });
        }

        toplamZam = decimal.Round(toplamZam, 2);
        string? not = null;
        if (toplamZam < 1m)
        {
            toplamZam = 1.00m; // m.51/2: gecikme zammı 1 TL'den az olamaz.
            not = "Hesaplanan zam 1 TL'nin altında kaldığından asgari 1 TL uygulandı (6183 m.51/2).";
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yontem = "6183 m.51: süre vade tarihinden ödeme tarihine kadardır; tam aylar aylık oranla, "
                + "ay kesirleri günlük (aylık oran / 30) hesaplanır, oran dönemleri ayrı ayrı uygulanır.",
            ToplamTamAy = toplamAy,
            ToplamKesirGun = toplamKesirGun,
            DonemKirilimi = kirilim,
            ToplamZam = toplamZam,
            GenelToplam = decimal.Round(p + toplamZam, 2),
            Not = not,
            Kaynak = DelaySurchargeRates.Kaynak,
        })));
    }
}

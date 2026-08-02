using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>İhbar tazminatı hesaplar (4857 sK m.17 süreleri, giydirilmiş brüt, gelir + damga vergisi).</summary>
public sealed class NoticePayTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "kidem_ay": { "type": "number", "description": "Kıdem süresi, ay olarak (tarihler verilmediyse zorunlu)" },
            "ise_giris": { "type": "string", "format": "date", "description": "İşe giriş tarihi (kidem_ay yerine tarih çifti verilebilir)" },
            "isten_cikis": { "type": "string", "format": "date", "description": "Fesih tarihi" },
            "brut_maas": { "type": "number", "description": "Son aylık brüt çıplak ücret, TL" },
            "yemek": { "type": "number", "description": "Aylık brüt yemek yardımı, TL (opsiyonel)" },
            "yol": { "type": "number", "description": "Aylık brüt yol yardımı, TL (opsiyonel)" },
            "diger": { "type": "number", "description": "Diğer düzenli aylık brüt yan haklar, TL (opsiyonel)" }
          },
          "required": ["brut_maas"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_ihbar_tazminati",
        "İhbar tazminatı hesaplar: 4857 sK m.17 bildirim süreleri (6 aydan az 2 hafta, 6-18 ay 4 hafta, 18-36 ay 6 hafta, 36 aydan fazla 8 hafta) x giydirilmiş günlük brüt ücret. İhbar tazminatı gelir vergisine ve damga vergisine tabidir (SGK primi kesilmez); gelir vergisi kesintisi yaklaşık hesaplanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var baseSalaryArg = ToolArgs.Decimal(arguments, "brut_maas");
        if (baseSalaryArg is not { } baseSalary || baseSalary <= 0)
        {
            return Task.FromResult(ToolResult.Error("Pozitif brut_maas zorunludur."));
        }

        decimal seniorityMonths;
        if (ToolArgs.Decimal(arguments, "kidem_ay") is { } months)
        {
            if (months < 0)
            {
                return Task.FromResult(ToolResult.Error("kidem_ay negatif olamaz."));
            }

            seniorityMonths = months;
        }
        else if (ToolArgs.Date(arguments, "ise_giris") is { } hireDate && ToolArgs.Date(arguments, "isten_cikis") is { } terminationDate)
        {
            if (hireDate >= terminationDate)
            {
                return Task.FromResult(ToolResult.Error("ise_giris, isten_cikis tarihinden önce olmalıdır."));
            }

            // 30 günlük ay yaklaşımı; süre basamağı belirlemek için yeterli hassasiyet.
            seniorityMonths = (terminationDate.DayNumber - hireDate.DayNumber) / 30m;
        }
        else
        {
            return Task.FromResult(ToolResult.Error("kidem_ay veya ise_giris + isten_cikis tarih çifti zorunludur."));
        }

        var mealAllowance = ToolArgs.Decimal(arguments, "yemek") ?? 0m;
        var transportAllowance = ToolArgs.Decimal(arguments, "yol") ?? 0m;
        var otherAllowances = ToolArgs.Decimal(arguments, "diger") ?? 0m;
        if (mealAllowance < 0 || transportAllowance < 0 || otherAllowances < 0)
        {
            return Task.FromResult(ToolResult.Error("yemek, yol ve diger negatif olamaz."));
        }

        var weeks = NoticeWeeks(seniorityMonths);
        var grossedUp = baseSalary + mealAllowance + transportAllowance + otherAllowances;
        var dailyWage = grossedUp / 30m;
        var grossNoticePay = dailyWage * 7m * weeks;

        // Gelir vergisi yaklaşık: tazminat tutarı 2026 ücret tarifesine tek başına uygulanır.
        // Gerçek kesinti, işçinin yıl içi kümülatif vergi matrahına göre değişir.
        var incomeTax = decimal.Round(PayrollParameters2026.TarifeVergisi(grossNoticePay), 2);
        var stampTax = decimal.Round(grossNoticePay * PayrollParameters2026.DamgaOrani, 2);
        var grossNoticePayRounded = decimal.Round(grossNoticePay, 2);

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            KidemAy = decimal.Round(seniorityMonths, 2),
            IhbarSuresiHafta = weeks,
            GiydirilmisBrut = decimal.Round(grossedUp, 2),
            GunlukUcret = decimal.Round(dailyWage, 2),
            BrutIhbarTazminati = grossNoticePayRounded,
            GelirVergisiYaklasik = incomeTax,
            DamgaVergisi = stampTax,
            NetIhbarTazminatiYaklasik = grossNoticePayRounded - incomeTax - stampTax,
            Not = "İhbar tazminatı gelir vergisine ve binde 7,59 damga vergisine tabidir, SGK primi kesilmez. Gelir vergisi burada tazminat tutarının 2026 ücret tarifesine tek başına uygulanmasıyla yaklaşık hesaplanmıştır; gerçek kesinti işçinin yıl içi kümülatif matrahındaki dilime göre değişir.",
            Kaynak = "4857 sK m.17; GVK m.61 ve m.103 (2026 tarifesi); 488 sK IV/1-b (binde 7,59)",
        })));
    }

    /// <summary>4857 m.17: 6 aydan az 2; 6 aydan 1,5 yıla kadar 4; 1,5 yıldan 3 yıla kadar 6; 3 yıldan fazla 8 hafta.</summary>
    private static int NoticeWeeks(decimal seniorityMonths) => seniorityMonths switch
    {
        < 6m => 2,
        < 18m => 4,
        <= 36m => 6,
        _ => 8,
    };
}

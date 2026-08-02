using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Kıdem tazminatı hesaplar (1475 sK m.14: giydirilmiş brüt, tavan kontrolü, damga kesintisi).</summary>
public sealed class SeveranceTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "ise_giris": { "type": "string", "format": "date", "description": "İşe giriş tarihi" },
            "isten_cikis": { "type": "string", "format": "date", "description": "İş sözleşmesinin sona erdiği tarih (fesih tarihi)" },
            "brut_maas": { "type": "number", "description": "Son aylık brüt çıplak ücret, TL" },
            "yemek": { "type": "number", "description": "Aylık brüt yemek yardımı, TL (opsiyonel)" },
            "yol": { "type": "number", "description": "Aylık brüt yol yardımı, TL (opsiyonel)" },
            "diger": { "type": "number", "description": "Diğer düzenli aylık brüt yan haklar (prim, ikramiye vb.), TL (opsiyonel)" }
          },
          "required": ["ise_giris", "isten_cikis", "brut_maas"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_kidem_tazminati",
        "Kıdem tazminatı hesaplar: giydirilmiş brüt ücret (çıplak brüt + yemek + yol + diğer yan haklar), fesih tarihindeki kıdem tavanı kontrolü, tam yıl + kalan gün orantısı ve damga vergisi kesintisi (binde 7,59). Kıdem tazminatından gelir vergisi ve SGK primi kesilmez.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var hireDateArg = ToolArgs.Date(arguments, "ise_giris");
        var terminationDateArg = ToolArgs.Date(arguments, "isten_cikis");
        var baseSalaryArg = ToolArgs.Decimal(arguments, "brut_maas");
        if (hireDateArg is not { } start || terminationDateArg is not { } end || baseSalaryArg is not { } baseSalary || baseSalary <= 0)
        {
            return Task.FromResult(ToolResult.Error("ise_giris, isten_cikis ve pozitif brut_maas zorunludur."));
        }

        if (start >= end)
        {
            return Task.FromResult(ToolResult.Error("ise_giris, isten_cikis tarihinden önce olmalıdır."));
        }

        var mealAllowance = ToolArgs.Decimal(arguments, "yemek") ?? 0m;
        var transportAllowance = ToolArgs.Decimal(arguments, "yol") ?? 0m;
        var otherAllowances = ToolArgs.Decimal(arguments, "diger") ?? 0m;
        if (mealAllowance < 0 || transportAllowance < 0 || otherAllowances < 0)
        {
            return Task.FromResult(ToolResult.Error("yemek, yol ve diger negatif olamaz."));
        }

        var totalDays = end.DayNumber - start.DayNumber;
        if (totalDays < 365)
        {
            return Task.FromResult(ToolResult.Error(
                "Kıdem 1 yıldan az: 1475 sK m.14 uyarınca kıdem tazminatı hakkı, en az 1 tam yıl çalışmayla doğar. Bu kıdemle kıdem tazminatı 0 TL'dir."));
        }

        if (SeveranceCeiling.At(end) is not { } ceilingPeriod)
        {
            return Task.FromResult(ToolResult.Error(
                $"Kıdem tavanı tablosu {SeveranceCeiling.Periods[0].From:dd.MM.yyyy} öncesini kapsamıyor; bu fesih tarihi için dönem tavanını elle doğrulayın."));
        }

        var grossedUp = baseSalary + mealAllowance + transportAllowance + otherAllowances;
        var ceilingApplied = grossedUp > ceilingPeriod.Amount;
        var baseWage = ceilingApplied ? ceilingPeriod.Amount : grossedUp;

        var fullYears = totalDays / 365;
        var remainingDays = totalDays % 365;
        var grossSeverance = (baseWage * fullYears) + (baseWage * remainingDays / 365m);
        var stampTax = decimal.Round(grossSeverance * PayrollParameters2026.DamgaOrani, 2);
        var grossSeveranceRounded = decimal.Round(grossSeverance, 2);

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            KidemSuresiGun = totalDays,
            TamYil = fullYears,
            KalanGun = remainingDays,
            GiydirilmisBrut = decimal.Round(grossedUp, 2),
            Tavan = ceilingPeriod.Amount,
            TavanDonemi = FormatPeriod(ceilingPeriod),
            TavanUygulandi = ceilingApplied,
            EsasUcret = decimal.Round(baseWage, 2),
            BrutKidemTazminati = grossSeveranceRounded,
            DamgaVergisi = stampTax,
            NetKidemTazminati = grossSeveranceRounded - stampTax,
            Not = "Kıdem tazminatından yalnızca binde 7,59 damga vergisi kesilir; gelir vergisi ve SGK primi kesilmez. Hesap, son giydirilmiş brüt ücretin fesih tarihindeki tavanla sınırlanmasıyla yapılır.",
            Kaynak = SeveranceCeiling.Kaynak,
        })));
    }

    private static string FormatPeriod(AmountPeriod p)
        => p.To is { } to ? $"{p.From:dd.MM.yyyy} - {to.AddDays(-1):dd.MM.yyyy}" : $"{p.From:dd.MM.yyyy} - devam ediyor";
}

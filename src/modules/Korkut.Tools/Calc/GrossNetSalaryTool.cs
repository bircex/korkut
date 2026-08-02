using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>2026 parametreleriyle brütten nete / netten brüte maaş hesaplar (asgari ücret istisnası dahil).</summary>
public sealed class GrossNetSalaryTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "yon": {
              "type": "string",
              "enum": ["brutten_nete", "netten_brute"],
              "description": "Hesap yönü"
            },
            "tutar": { "type": "number", "description": "yon=brutten_nete ise aylık brüt, yon=netten_brute ise hedef aylık net, TL" },
            "ay": { "type": "integer", "description": "Yılın kaçıncı ayı (1-12, varsayılan 1); kümülatif vergi matrahı bu ay konumuna göre, tüm aylarda aynı brüt varsayımıyla hesaplanır" }
          },
          "required": ["yon", "tutar"]
        }
        """);

    private static readonly string[] Assumptions =
    [
        "2026 parametreleri: brüt asgari ücret 33.030,00 TL, SGK işçi %14 + işsizlik %1, SGK tavanı 247.725,00 TL/ay, damga binde 7,59",
        "Gelir vergisi 2026 ücret tarifesi; kümülatif matrah, tüm aylarda aynı brüt ücret varsayımıyla ay konumuna göre hesaplanır",
        "Asgari ücret istisnası uygulanır: ücretin asgari ücrete isabet eden kısmının gelir ve damga vergisi alınmaz",
        "Tek işveren, 4/a sigortalısı; engellilik indirimi, BES kesintisi, özel sigorta ve teşvikler hesaba katılmamıştır",
    ];

    public ToolSpec Spec => new(
        "calc_brut_net_maas",
        "2026 bordro parametreleriyle brütten nete veya netten brüte maaş hesaplar: SGK işçi %14 + işsizlik %1 (tavan 247.725 TL), 2026 gelir vergisi ücret tarifesi, damga binde 7,59 ve asgari ücret istisnası. Netten brüte iteratif çözülür; yıllık projeksiyon da verilir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var direction = ToolArgs.String(arguments, "yon");
        var amountArg = ToolArgs.Decimal(arguments, "tutar");
        if (direction is not ("brutten_nete" or "netten_brute") || amountArg is not { } amount || amount <= 0)
        {
            return Task.FromResult(ToolResult.Error("yon (brutten_nete | netten_brute) ve pozitif tutar zorunludur."));
        }

        var month = ToolArgs.Int(arguments, "ay") ?? 1;
        if (month is < 1 or > 12)
        {
            return Task.FromResult(ToolResult.Error("ay 1 ile 12 arasında olmalıdır."));
        }

        var grossAmount = direction == "brutten_nete" ? amount : NetToGross(amount, month);
        var payroll = Calculate(grossAmount, month);

        decimal annualNet = 0m;
        for (var m = 1; m <= 12; m++)
        {
            annualNet += Calculate(grossAmount, m).Net;
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yon = direction,
            Ay = month,
            BrutAylik = decimal.Round(payroll.Gross, 2),
            NetAylik = decimal.Round(payroll.Net, 2),
            Kesintiler = new
            {
                SgkIsci = payroll.SocialSecurity,
                IssizlikIsci = payroll.Unemployment,
                GelirVergisi = payroll.IncomeTax,
                DamgaVergisi = payroll.StampTax,
                Toplam = payroll.SocialSecurity + payroll.Unemployment + payroll.IncomeTax + payroll.StampTax,
            },
            AsgariUcretIstisnasi = new
            {
                GelirVergisi = payroll.IncomeTaxExemption,
                DamgaVergisi = payroll.StampTaxExemption,
            },
            YillikProjeksiyon = new
            {
                YillikBrut = decimal.Round(grossAmount * 12m, 2),
                YillikNet = decimal.Round(annualNet, 2),
                Aciklama = "Tüm yıl aynı brüt ücretle çalışıldığı varsayılarak 12 ayın kümülatif vergili net toplamı",
            },
            Varsayimlar = Assumptions,
            TarifeDonemi = "2026",
            Kaynak = PayrollParameters2026.Kaynak,
        })));
    }

    private sealed record MonthlyPayroll(
        decimal Gross, decimal SocialSecurity, decimal Unemployment, decimal IncomeTax, decimal StampTax,
        decimal IncomeTaxExemption, decimal StampTaxExemption, decimal Net);

    /// <summary>Ay konumuna göre tek ay bordrosu (kümülatif matrah = matrah x ay basitleştirmesi).</summary>
    private static MonthlyPayroll Calculate(decimal grossAmount, int month)
    {
        var socialSecurityBase = Math.Min(grossAmount, PayrollParameters2026.SgkTavanAylik);
        var socialSecurity = decimal.Round(socialSecurityBase * PayrollParameters2026.SgkIsciOrani, 2);
        var unemployment = decimal.Round(socialSecurityBase * PayrollParameters2026.IssizlikIsciOrani, 2);
        var taxBase = grossAmount - socialSecurity - unemployment;

        var incomeTaxFull = PayrollParameters2026.TarifeVergisi(taxBase * month)
            - PayrollParameters2026.TarifeVergisi(taxBase * (month - 1));

        // İstisna: asgari ücretlinin aynı ay konumunda ödeyeceği vergi kadar (GVK m.23/18).
        var exemptionBase = Math.Min(taxBase, PayrollParameters2026.AsgariUcretBrut * 0.85m);
        var exemption = PayrollParameters2026.TarifeVergisi(exemptionBase * month)
            - PayrollParameters2026.TarifeVergisi(exemptionBase * (month - 1));
        var incomeTax = decimal.Round(Math.Max(0m, incomeTaxFull - exemption), 2);

        var stampExemptionBase = Math.Min(grossAmount, PayrollParameters2026.AsgariUcretBrut);
        var stampTax = decimal.Round(
            Math.Max(0m, (grossAmount - stampExemptionBase) * PayrollParameters2026.DamgaOrani), 2);

        var net = grossAmount - socialSecurity - unemployment - incomeTax - stampTax;
        return new MonthlyPayroll(
            grossAmount, socialSecurity, unemployment, incomeTax, stampTax,
            decimal.Round(Math.Min(incomeTaxFull, exemption), 2),
            decimal.Round(stampExemptionBase * PayrollParameters2026.DamgaOrani, 2),
            net);
    }

    /// <summary>Hedef neti veren brütü ikili aramayla çözer (net, brüte göre artandır).</summary>
    private static decimal NetToGross(decimal targetNet, int month)
    {
        var lo = targetNet;
        var hi = targetNet * 2m;
        for (var i = 0; i < 80; i++)
        {
            var mid = (lo + hi) / 2m;
            if (Calculate(mid, month).Net < targetNet)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return decimal.Round(hi, 2);
    }
}

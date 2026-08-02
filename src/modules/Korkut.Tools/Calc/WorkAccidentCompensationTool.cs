using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// İş kazasında işçinin sürekli/geçici iş göremezlik (maddi) tazminatını mahkeme modunda hesaplar.
/// Aktüeryal yöntem bedensel tazminatla aynıdır (TRH-2010, net teknik faiz %0, aktif dönem sonu 60,
/// pasif dönem AGİ hariç net asgari ücret); tazminat işverenin kusur oranıyla çarpılır ve SGK
/// tarafından sürekli iş göremezlik geliri bağlandıysa peşin sermaye değeri düşülür.
/// </summary>
public sealed class WorkAccidentCompensationTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "cinsiyet": { "type": "string", "enum": ["erkek", "kadin"], "description": "İşçinin cinsiyeti (TRH-2010 bakiye ömrü için)" },
            "yas": { "type": "integer", "description": "Kaza tarihindeki yaş (0-100)" },
            "surekli_is_goremezlik_orani": { "type": "number", "description": "Sürekli iş göremezlik (maluliyet) oranı, yüzde (0-100)" },
            "isveren_kusur_orani": { "type": "number", "description": "İşverenin kusur oranı, yüzde (0-100); tazminat bu oranla çarpılır. Verilmezse 100 alınır" },
            "aylik_net_gelir": { "type": "number", "description": "İşçinin aylık net ücreti (TL); verilmezse AGİ hariç net asgari ücret alınır (2026: 28.075,50 TL)" },
            "gecici_is_goremezlik_ay": { "type": "number", "description": "Geçici iş göremezlik (istirahat) süresi, ay; bu dönem %100 iş göremezlikle hesaplanır" },
            "sgk_gelir_baglandi": { "type": "boolean", "description": "SGK sürekli iş göremezlik geliri bağladı mı" },
            "sgk_pesin_sermaye": { "type": "number", "description": "SGK gelirinin peşin sermaye değeri (TL); verilirse tazminattan düşülür" }
          },
          "required": ["cinsiyet", "yas", "surekli_is_goremezlik_orani"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_is_kazasi_tazminati",
        "İş kazasında işçinin maddi (sürekli/geçici iş göremezlik) tazminatını mahkeme modunda hesaplar: TRH-2010 bakiye ömür, progresif rant (net teknik faiz %0), aktif dönem 60 yaşa kadar, pasif dönem AGİ hariç net asgari ücret; tazminat işverenin kusur oranıyla çarpılır, SGK peşin sermaye değeri verilirse düşülür.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var gender = Trh2010.NormalizeCinsiyet(ToolArgs.String(arguments, "cinsiyet"));
        var age = ToolArgs.Int(arguments, "yas");
        var disabilityRate = ToolArgs.Decimal(arguments, "surekli_is_goremezlik_orani");
        if (gender is null || age is not { } ageValue || disabilityRate is not { } disabilityValue || disabilityValue <= 0 || disabilityValue > 100)
        {
            return Task.FromResult(ToolResult.Error(
                "cinsiyet (erkek|kadin), yas ve 0-100 arası surekli_is_goremezlik_orani zorunludur."));
        }

        var employerFaultRate = ToolArgs.Decimal(arguments, "isveren_kusur_orani") ?? 100m;
        if (employerFaultRate is <= 0 or > 100)
        {
            return Task.FromResult(ToolResult.Error("isveren_kusur_orani 0'dan büyük ve en çok 100 olmalıdır."));
        }

        var temporaryDisabilityMonths = ToolArgs.Decimal(arguments, "gecici_is_goremezlik_ay") ?? 0m;
        if (temporaryDisabilityMonths < 0)
        {
            return Task.FromResult(ToolResult.Error("gecici_is_goremezlik_ay negatif olamaz."));
        }

        var monthlyIncome = ToolArgs.Decimal(arguments, "aylik_net_gelir") ?? MinimumWage.GuncelAylikNet;
        if (monthlyIncome <= 0)
        {
            return Task.FromResult(ToolResult.Error("aylik_net_gelir pozitif olmalıdır."));
        }

        var capitalizedValue = ToolArgs.Decimal(arguments, "sgk_pesin_sermaye");
        if (capitalizedValue is { } capitalizedCheck && capitalizedCheck < 0)
        {
            return Task.FromResult(ToolResult.Error("sgk_pesin_sermaye negatif olamaz."));
        }

        if (ActuarialMethod.Hesapla(gender, ageValue) is not { } period)
        {
            return Task.FromResult(ToolResult.Error("yas 0-100 aralığında olmalıdır."));
        }

        var disabilityFraction = disabilityValue / 100m;
        var annualIncome = monthlyIncome * 12m;

        var activeLoss = decimal.Round(annualIncome * period.AktifYil * disabilityFraction, 2);
        var passiveLoss = decimal.Round(MinimumWage.GuncelYillikNet * period.PasifYil * disabilityFraction, 2);
        var temporaryLoss = decimal.Round(monthlyIncome * temporaryDisabilityMonths, 2);
        var grossTotal = activeLoss + passiveLoss + temporaryLoss;
        var compensationAfterFault = decimal.Round(grossTotal * (employerFaultRate / 100m), 2);
        var socialSecurityDeduction = capitalizedValue is { } capitalAmount ? decimal.Round(Math.Min(capitalAmount, compensationAfterFault), 2) : 0m;
        var netCompensation = compensationAfterFault - socialSecurityDeduction;

        var assumptions = new List<string>
        {
            "Bakiye ömür TRH-2010 tablosundan, cinsiyet ve kaza yaşına göre alındı.",
            "Mahkeme modu progresif rant: %10 artırım + %10 iskonto birbirini götürür, net teknik faiz %0; toplam kayıp = yıllık kayıp x yıl.",
            $"Aktif dönem kaza yaşından {ActuarialMethod.AktifDonemSonuYas} yaşına kadardır; {ActuarialMethod.AktifDonemSonuYas} ve üzeri yaşta aktif dönem 0 alınır (çalışmanın sürdüğü belgelenirse hesap değişir).",
            $"Pasif dönem geliri AGİ hariç net asgari ücrettir ({MinimumWage.GuncelAylikNet:N2} TL/ay).",
            "Geçici iş göremezlik döneminde %100 iş göremezlik ve işçinin kendi ücreti esas alındı.",
            "Tazminat işverenin kusur oranıyla çarpılır; işçinin müterafik kusuru ve kaçınılmazlık indirimi bu orana yansıtılmalıdır.",
        };
        if (ToolArgs.Bool(arguments, "sgk_gelir_baglandi") && capitalizedValue is null)
        {
            assumptions.Add(
                "SGK sürekli iş göremezlik geliri bağlandığı bildirildi ancak peşin sermaye değeri verilmedi: SGK'nın bildireceği ilk peşin sermaye değeri tazminattan ayrıca düşülmelidir; buradaki sonuç indirimsizdir.");
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yontem = "İş kazası maddi tazminatı, mahkeme modu (TRH-2010 + progresif rant %0 net teknik faiz)",
            BakiyeOmurYil = decimal.Round(period.BakiyeOmur, 2),
            AktifDonemYil = decimal.Round(period.AktifYil, 2),
            PasifDonemYil = decimal.Round(period.PasifYil, 2),
            EsasAlinanAylikNetGelir = decimal.Round(monthlyIncome, 2),
            AktifDonemKaybi = activeLoss,
            PasifDonemKaybi = passiveLoss,
            GeciciIsGoremezlikKaybi = temporaryLoss,
            BrutToplam = grossTotal,
            IsverenKusurOrani = employerFaultRate,
            KusurSonrasiTazminat = compensationAfterFault,
            SgkPesinSermayeIndirimi = socialSecurityDeduction,
            NetTazminat = netCompensation,
            Uyari = "Bilirkişi hesabı ücret artışları, belgeli çalışma süresi ve SGK peşin sermaye güncellemeleriyle bu sonuçtan sapabilir; sonuç yaklaşık değerdir.",
            Varsayimlar = assumptions,
            Kaynak = $"{Trh2010.Kaynak}; {MinimumWage.Kaynak}",
        })));
    }
}

using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// Yaralanmalı trafik kazasında sürekli/geçici iş göremezlik (bedensel) tazminatını mahkeme
/// modunda hesaplar: TRH-2010 bakiye ömür, progresif rant (%10 artırım + %10 iskonto, net teknik
/// faiz %0), aktif dönem sonu 60 yaş, pasif dönem AGİ hariç net asgari ücret.
/// </summary>
public sealed class BodilyInjuryCompensationTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "cinsiyet": { "type": "string", "enum": ["erkek", "kadin"], "description": "Mağdurun cinsiyeti (TRH-2010 bakiye ömrü için)" },
            "yas": { "type": "integer", "description": "Kaza tarihindeki yaş (0-100)" },
            "maluliyet_orani": { "type": "number", "description": "Sürekli maluliyet (iş göremezlik) oranı, yüzde (0-100)" },
            "kusur_orani": { "type": "number", "description": "MAĞDURUN kendi kusuru, yüzde (0-100); karşı tarafın kusuru DEĞİL. Verilmezse 0 alınır" },
            "aylik_net_gelir": { "type": "number", "description": "Mağdurun aylık net geliri (TL); verilmezse AGİ hariç net asgari ücret alınır (2026: 28.075,50 TL)" },
            "gecici_is_goremezlik_ay": { "type": "number", "description": "Geçici iş göremezlik (iyileşme) süresi, ay; bu dönem %100 iş göremezlikle hesaplanır" }
          },
          "required": ["cinsiyet", "yas", "maluliyet_orani"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_bedensel_tazminat",
        "Yaralanmalı trafik kazasında bedensel (sürekli/geçici iş göremezlik) tazminatını mahkeme modunda hesaplar: TRH-2010 bakiye ömür, progresif rant (net teknik faiz %0), aktif dönem 60 yaşa kadar, pasif dönem AGİ hariç net asgari ücretten; mağdur kusuru düşülür.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var gender = Trh2010.NormalizeCinsiyet(ToolArgs.String(arguments, "cinsiyet"));
        var age = ToolArgs.Int(arguments, "yas");
        var disabilityRate = ToolArgs.Decimal(arguments, "maluliyet_orani");
        if (gender is null || age is not { } ageValue || disabilityRate is not { } disabilityValue || disabilityValue <= 0 || disabilityValue > 100)
        {
            return Task.FromResult(ToolResult.Error(
                "cinsiyet (erkek|kadin), yas ve 0-100 arası maluliyet_orani zorunludur."));
        }

        var faultRate = ToolArgs.Decimal(arguments, "kusur_orani") ?? 0m;
        if (faultRate is < 0 or > 100)
        {
            return Task.FromResult(ToolResult.Error("kusur_orani 0-100 arasında olmalıdır."));
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

        if (ActuarialMethod.Hesapla(gender, ageValue) is not { } period)
        {
            return Task.FromResult(ToolResult.Error("yas 0-100 aralığında olmalıdır."));
        }

        var disabilityFraction = disabilityValue / 100m;
        var faultFraction = faultRate / 100m;
        var annualIncome = monthlyIncome * 12m;

        var activeLoss = decimal.Round(annualIncome * period.AktifYil * disabilityFraction, 2);
        var passiveLoss = decimal.Round(MinimumWage.GuncelYillikNet * period.PasifYil * disabilityFraction, 2);
        var temporaryLoss = decimal.Round(monthlyIncome * temporaryDisabilityMonths, 2);
        var grossTotal = activeLoss + passiveLoss + temporaryLoss;
        var faultDeduction = decimal.Round(grossTotal * faultFraction, 2);
        var netCompensation = grossTotal - faultDeduction;

        var assumptions = new List<string>
        {
            "Bakiye ömür TRH-2010 tablosundan, cinsiyet ve kaza yaşına göre alındı.",
            "Mahkeme modu progresif rant: %10 artırım + %10 iskonto birbirini götürür, net teknik faiz %0; toplam kayıp = yıllık kayıp x yıl.",
            $"Aktif dönem kaza yaşından {ActuarialMethod.AktifDonemSonuYas} yaşına kadardır; {ActuarialMethod.AktifDonemSonuYas} ve üzeri yaşta aktif dönem 0 alınır (çalışmanın sürdüğü belgelenirse hesap değişir).",
            $"Pasif dönem geliri AGİ hariç net asgari ücrettir ({MinimumWage.GuncelAylikNet:N2} TL/ay).",
            "Geçici iş göremezlik döneminde %100 iş göremezlik ve mağdurun kendi geliri esas alındı.",
            "Kusur indirimi mağdurun kusur oranıdır; karşı tarafın kusuru girilmemelidir.",
        };
        if (ToolArgs.Decimal(arguments, "aylik_net_gelir") is null)
        {
            assumptions.Add("aylik_net_gelir verilmediğinden aktif dönem için de AGİ hariç net asgari ücret esas alındı.");
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yontem = "Aktüeryal bedensel tazminat, mahkeme modu (TRH-2010 + progresif rant %0 net teknik faiz)",
            BakiyeOmurYil = decimal.Round(period.BakiyeOmur, 2),
            AktifDonemYil = decimal.Round(period.AktifYil, 2),
            PasifDonemYil = decimal.Round(period.PasifYil, 2),
            EsasAlinanAylikNetGelir = decimal.Round(monthlyIncome, 2),
            AktifDonemKaybi = activeLoss,
            PasifDonemKaybi = passiveLoss,
            GeciciIsGoremezlikKaybi = temporaryLoss,
            BrutToplam = grossTotal,
            KusurOrani = faultRate,
            KusurIndirimi = faultDeduction,
            NetTazminat = netCompensation,
            ZmssLimitNotu = $"ZMSS (trafik sigortası) 2026 kişi başı sakatlık teminat limiti {Zmss.KisiBasiTeminatLimiti2026:N2} TL'dir; sigortacıdan talep bu limitle sınırlıdır, aşan kısım işletene/sürücüye yöneltilir.",
            Varsayimlar = assumptions,
            Kaynak = $"{Trh2010.Kaynak}; {MinimumWage.Kaynak}; {Zmss.TeminatKaynak}",
        })));
    }
}

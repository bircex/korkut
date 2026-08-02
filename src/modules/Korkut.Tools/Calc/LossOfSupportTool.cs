using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// Ölümlü olayda destekten yoksun kalma tazminatını mahkeme modunda hesaplar: TRH-2010 bakiye
/// ömür, progresif rant (net teknik faiz %0), aktif dönem sonu 60 yaş, pasif dönem AGİ hariç net
/// asgari ücret. Pay dağılımı yerleşik uygulamadır: müteveffa 2, eş 2, her çocuk 1, anne/baba 1'er
/// pay; müteveffanın payı kendi tüketimi olarak dağıtılmaz (klasik havuz yöntemi).
/// </summary>
public sealed class LossOfSupportTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "cinsiyet": { "type": "string", "enum": ["erkek", "kadin"], "description": "Müteveffanın cinsiyeti (TRH-2010 bakiye ömrü için)" },
            "yas": { "type": "integer", "description": "Müteveffanın ölüm tarihindeki yaşı (0-100)" },
            "aylik_net_gelir": { "type": "number", "description": "Müteveffanın aylık net geliri (TL); verilmezse AGİ hariç net asgari ücret alınır (2026: 28.075,50 TL)" },
            "kusur_orani": { "type": "number", "description": "MÜTEVEFFANIN kendi kusuru, yüzde (0-100); verilmezse 0 alınır" },
            "es_var": { "type": "boolean", "description": "Sağ kalan eş var mı" },
            "es_yasi": { "type": "integer", "description": "Eşin yaşı; verilmezse müteveffanın yaşıyla aynı varsayılır" },
            "cocuklar": { "type": "array", "items": { "type": "integer" }, "description": "Çocukların yaşları (destek 18 yaşına kadar)" },
            "anne_var": { "type": "boolean", "description": "Destek gören anne var mı" },
            "anne_yasi": { "type": "integer", "description": "Annenin yaşı; verilmezse müteveffanın yaşı + 25 varsayılır" },
            "baba_var": { "type": "boolean", "description": "Destek gören baba var mı" },
            "baba_yasi": { "type": "integer", "description": "Babanın yaşı; verilmezse müteveffanın yaşı + 25 varsayılır" }
          },
          "required": ["cinsiyet", "yas"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_destekten_yoksunluk",
        "Ölümlü olayda (trafik kazası vb.) destekten yoksun kalma tazminatını mahkeme modunda hak sahibi bazında hesaplar: TRH-2010 bakiye ömür, progresif rant (net teknik faiz %0), pay dağılımı müteveffa 2 / eş 2 / çocuk 1 / anne-baba 1'er; müteveffa kusuru düşülür.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var gender = Trh2010.NormalizeCinsiyet(ToolArgs.String(arguments, "cinsiyet"));
        var age = ToolArgs.Int(arguments, "yas");
        if (gender is null || age is not { } ageValue)
        {
            return Task.FromResult(ToolResult.Error("cinsiyet (erkek|kadin) ve yas zorunludur."));
        }

        var faultRate = ToolArgs.Decimal(arguments, "kusur_orani") ?? 0m;
        if (faultRate is < 0 or > 100)
        {
            return Task.FromResult(ToolResult.Error("kusur_orani 0-100 arasında olmalıdır."));
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

        var childAges = ChildAges(arguments);
        if (childAges is null)
        {
            return Task.FromResult(ToolResult.Error("cocuklar tam sayı yaş listesi olmalıdır (0-100)."));
        }

        var hasSpouse = ToolArgs.Bool(arguments, "es_var");
        var hasMother = ToolArgs.Bool(arguments, "anne_var");
        var hasFather = ToolArgs.Bool(arguments, "baba_var");
        if (!hasSpouse && !hasMother && !hasFather && childAges.Length == 0)
        {
            return Task.FromResult(ToolResult.Error(
                "En az bir hak sahibi (es_var, cocuklar, anne_var, baba_var) belirtilmelidir."));
        }

        var assumptions = new List<string>
        {
            "Bakiye ömürler TRH-2010 tablosundan alındı; destek süresi her hak sahibi için müteveffanın bakiye ömrüyle sınırlıdır.",
            "Mahkeme modu progresif rant: %10 artırım + %10 iskonto birbirini götürür, net teknik faiz %0; destek = yıllık destek x yıl.",
            $"Aktif dönem müteveffanın {ActuarialMethod.AktifDonemSonuYas} yaşına kadardır; pasif dönemde AGİ hariç net asgari ücret ({MinimumWage.GuncelAylikNet:N2} TL/ay) esas alınır.",
            "Pay dağılımı yerleşik uygulama: müteveffa 2, eş 2, her çocuk 1, anne ve baba 1'er pay; müteveffanın payı kendi tüketimi sayılır ve dağıtılmaz.",
            "Çocuklara destek 18 yaşına kadar hesaplanır (kız/erkek ayrımı ve eğitim uzatması bu sürümde yok).",
            "Eşin yeniden evlenme ihtimali indirimi bu sürümde uygulanmamaktadır; bilirkişi hesabında AYİM/TRH pratiğine göre indirim yapılabilir.",
        };

        // Hak sahipleri ve pay havuzu (müteveffa 2 pay ile havuzda kalır, kendi payı dağıtılmaz).
        var beneficiaries = new List<(string Name, int Age, string Gender, int Share, decimal? SupportYears)>();
        if (hasSpouse)
        {
            var spouseAge = ToolArgs.Int(arguments, "es_yasi") ?? ageValue;
            if (ToolArgs.Int(arguments, "es_yasi") is null)
            {
                assumptions.Add("es_yasi verilmediğinden eş müteveffayla aynı yaşta varsayıldı.");
            }

            var spouseGender = gender == "erkek" ? "kadin" : "erkek";
            assumptions.Add("Eşin cinsiyeti müteveffanın karşı cinsi varsayıldı (bakiye ömür tablosu seçimi için).");
            beneficiaries.Add(("es", spouseAge, spouseGender, 2, null));
        }

        for (var i = 0; i < childAges.Length; i++)
        {
            beneficiaries.Add(($"cocuk_{i + 1}", childAges[i], gender, 1, Math.Max(0m, 18 - childAges[i])));
        }

        if (hasMother)
        {
            var motherAge = ToolArgs.Int(arguments, "anne_yasi") ?? Math.Min(ageValue + 25, 100);
            if (ToolArgs.Int(arguments, "anne_yasi") is null)
            {
                assumptions.Add("anne_yasi verilmediğinden müteveffanın yaşı + 25 varsayıldı.");
            }

            beneficiaries.Add(("anne", motherAge, "kadin", 1, null));
        }

        if (hasFather)
        {
            var fatherAge = ToolArgs.Int(arguments, "baba_yasi") ?? Math.Min(ageValue + 25, 100);
            if (ToolArgs.Int(arguments, "baba_yasi") is null)
            {
                assumptions.Add("baba_yasi verilmediğinden müteveffanın yaşı + 25 varsayıldı.");
            }

            beneficiaries.Add(("baba", fatherAge, "erkek", 1, null));
        }

        var totalShares = 2 + beneficiaries.Sum(b => b.Share);
        var faultFraction = faultRate / 100m;
        var annualIncome = monthlyIncome * 12m;
        var annualMinimumWage = MinimumWage.GuncelYillikNet;

        decimal grossTotal = 0, netTotal = 0;
        var breakdown = new List<object>(beneficiaries.Count);
        foreach (var (name, beneficiaryAge, beneficiaryGender, share, fixedSupportYears) in beneficiaries)
        {
            decimal supportYears;
            if (fixedSupportYears is { } childFixedYears)
            {
                supportYears = Math.Min(childFixedYears, period.BakiyeOmur);
            }
            else if (Trh2010.BakiyeOmur(beneficiaryGender, beneficiaryAge) is { } ownResidualLife)
            {
                supportYears = Math.Min(ownResidualLife, period.BakiyeOmur);
            }
            else
            {
                return Task.FromResult(ToolResult.Error($"{name} için yaş 0-100 aralığında olmalıdır."));
            }

            var activePart = Math.Min(supportYears, period.AktifYil);
            var passivePart = Math.Max(0m, supportYears - activePart);
            var shareFraction = (decimal)share / totalShares;
            var gross = decimal.Round((annualIncome * activePart + annualMinimumWage * passivePart) * shareFraction, 2);
            var net = decimal.Round(gross * (1m - faultFraction), 2);
            grossTotal += gross;
            netTotal += net;
            breakdown.Add(new
            {
                HakSahibi = name,
                Yas = beneficiaryAge,
                Pay = share,
                DestekYili = decimal.Round(supportYears, 2),
                AktifYil = decimal.Round(activePart, 2),
                PasifYil = decimal.Round(passivePart, 2),
                BrutDestek = gross,
                NetDestek = net,
            });
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yontem = "Destekten yoksun kalma tazminatı, mahkeme modu (TRH-2010 + progresif rant %0 net teknik faiz, klasik pay havuzu)",
            MuteveffaBakiyeOmurYil = decimal.Round(period.BakiyeOmur, 2),
            AktifDonemYil = decimal.Round(period.AktifYil, 2),
            PasifDonemYil = decimal.Round(period.PasifYil, 2),
            EsasAlinanAylikNetGelir = decimal.Round(monthlyIncome, 2),
            ToplamPay = totalShares,
            KusurOrani = faultRate,
            HakSahipleri = breakdown,
            BrutToplam = grossTotal,
            NetToplam = netTotal,
            Varsayimlar = assumptions,
            Kaynak = $"{Trh2010.Kaynak}; {MinimumWage.Kaynak}",
        })));
    }

    /// <summary>cocuklar dizisini okur; yoksa boş dizi, biçim bozuksa null.</summary>
    private static int[]? ChildAges(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("cocuklar", out var arr) || arr.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (arr.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var ages = new int[arr.GetArrayLength()];
        var i = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (!item.TryGetInt32(out var childAge) || childAge is < 0 or > 100)
            {
                return null;
            }

            ages[i++] = childAge;
        }

        return ages;
    }
}

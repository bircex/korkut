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
public sealed class DestektenYoksunlukTool : IAgentTool
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
        var cinsiyet = Trh2010.NormalizeCinsiyet(ToolArgs.String(arguments, "cinsiyet"));
        var yas = ToolArgs.Int(arguments, "yas");
        if (cinsiyet is null || yas is not { } y)
        {
            return Task.FromResult(ToolResult.Error("cinsiyet (erkek|kadin) ve yas zorunludur."));
        }

        var kusur = ToolArgs.Decimal(arguments, "kusur_orani") ?? 0m;
        if (kusur is < 0 or > 100)
        {
            return Task.FromResult(ToolResult.Error("kusur_orani 0-100 arasında olmalıdır."));
        }

        var aylikGelir = ToolArgs.Decimal(arguments, "aylik_net_gelir") ?? MinimumWage.GuncelAylikNet;
        if (aylikGelir <= 0)
        {
            return Task.FromResult(ToolResult.Error("aylik_net_gelir pozitif olmalıdır."));
        }

        if (ActuarialMethod.Hesapla(cinsiyet, y) is not { } donem)
        {
            return Task.FromResult(ToolResult.Error("yas 0-100 aralığında olmalıdır."));
        }

        var cocukYaslari = CocukYaslari(arguments);
        if (cocukYaslari is null)
        {
            return Task.FromResult(ToolResult.Error("cocuklar tam sayı yaş listesi olmalıdır (0-100)."));
        }

        var esVar = ToolArgs.Bool(arguments, "es_var");
        var anneVar = ToolArgs.Bool(arguments, "anne_var");
        var babaVar = ToolArgs.Bool(arguments, "baba_var");
        if (!esVar && !anneVar && !babaVar && cocukYaslari.Length == 0)
        {
            return Task.FromResult(ToolResult.Error(
                "En az bir hak sahibi (es_var, cocuklar, anne_var, baba_var) belirtilmelidir."));
        }

        var varsayimlar = new List<string>
        {
            "Bakiye ömürler TRH-2010 tablosundan alındı; destek süresi her hak sahibi için müteveffanın bakiye ömrüyle sınırlıdır.",
            "Mahkeme modu progresif rant: %10 artırım + %10 iskonto birbirini götürür, net teknik faiz %0; destek = yıllık destek x yıl.",
            $"Aktif dönem müteveffanın {ActuarialMethod.AktifDonemSonuYas} yaşına kadardır; pasif dönemde AGİ hariç net asgari ücret ({MinimumWage.GuncelAylikNet:N2} TL/ay) esas alınır.",
            "Pay dağılımı yerleşik uygulama: müteveffa 2, eş 2, her çocuk 1, anne ve baba 1'er pay; müteveffanın payı kendi tüketimi sayılır ve dağıtılmaz.",
            "Çocuklara destek 18 yaşına kadar hesaplanır (kız/erkek ayrımı ve eğitim uzatması bu sürümde yok).",
            "Eşin yeniden evlenme ihtimali indirimi bu sürümde uygulanmamaktadır; bilirkişi hesabında AYİM/TRH pratiğine göre indirim yapılabilir.",
        };

        // Hak sahipleri ve pay havuzu (müteveffa 2 pay ile havuzda kalır, kendi payı dağıtılmaz).
        var sahipler = new List<(string Ad, int Yas, string Cinsiyet, int Pay, decimal? DestekYili)>();
        if (esVar)
        {
            var esYasi = ToolArgs.Int(arguments, "es_yasi") ?? y;
            if (ToolArgs.Int(arguments, "es_yasi") is null)
            {
                varsayimlar.Add("es_yasi verilmediğinden eş müteveffayla aynı yaşta varsayıldı.");
            }

            var esCinsiyet = cinsiyet == "erkek" ? "kadin" : "erkek";
            varsayimlar.Add("Eşin cinsiyeti müteveffanın karşı cinsi varsayıldı (bakiye ömür tablosu seçimi için).");
            sahipler.Add(("es", esYasi, esCinsiyet, 2, null));
        }

        for (var i = 0; i < cocukYaslari.Length; i++)
        {
            sahipler.Add(($"cocuk_{i + 1}", cocukYaslari[i], cinsiyet, 1, Math.Max(0m, 18 - cocukYaslari[i])));
        }

        if (anneVar)
        {
            var anneYasi = ToolArgs.Int(arguments, "anne_yasi") ?? Math.Min(y + 25, 100);
            if (ToolArgs.Int(arguments, "anne_yasi") is null)
            {
                varsayimlar.Add("anne_yasi verilmediğinden müteveffanın yaşı + 25 varsayıldı.");
            }

            sahipler.Add(("anne", anneYasi, "kadin", 1, null));
        }

        if (babaVar)
        {
            var babaYasi = ToolArgs.Int(arguments, "baba_yasi") ?? Math.Min(y + 25, 100);
            if (ToolArgs.Int(arguments, "baba_yasi") is null)
            {
                varsayimlar.Add("baba_yasi verilmediğinden müteveffanın yaşı + 25 varsayıldı.");
            }

            sahipler.Add(("baba", babaYasi, "erkek", 1, null));
        }

        var toplamPay = 2 + sahipler.Sum(s => s.Pay);
        var kusurOran = kusur / 100m;
        var yillikGelir = aylikGelir * 12m;
        var yillikAsgari = MinimumWage.GuncelYillikNet;

        decimal brutToplam = 0, netToplam = 0;
        var dokum = new List<object>(sahipler.Count);
        foreach (var (ad, sYas, sCinsiyet, pay, sabitSure) in sahipler)
        {
            decimal sure;
            if (sabitSure is { } cocukSure)
            {
                sure = Math.Min(cocukSure, donem.BakiyeOmur);
            }
            else if (Trh2010.BakiyeOmur(sCinsiyet, sYas) is { } kendiBakiye)
            {
                sure = Math.Min(kendiBakiye, donem.BakiyeOmur);
            }
            else
            {
                return Task.FromResult(ToolResult.Error($"{ad} için yaş 0-100 aralığında olmalıdır."));
            }

            var aktifPart = Math.Min(sure, donem.AktifYil);
            var pasifPart = Math.Max(0m, sure - aktifPart);
            var payOrani = (decimal)pay / toplamPay;
            var brut = decimal.Round((yillikGelir * aktifPart + yillikAsgari * pasifPart) * payOrani, 2);
            var net = decimal.Round(brut * (1m - kusurOran), 2);
            brutToplam += brut;
            netToplam += net;
            dokum.Add(new
            {
                HakSahibi = ad,
                Yas = sYas,
                Pay = pay,
                DestekYili = decimal.Round(sure, 2),
                AktifYil = decimal.Round(aktifPart, 2),
                PasifYil = decimal.Round(pasifPart, 2),
                BrutDestek = brut,
                NetDestek = net,
            });
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yontem = "Destekten yoksun kalma tazminatı, mahkeme modu (TRH-2010 + progresif rant %0 net teknik faiz, klasik pay havuzu)",
            MuteveffaBakiyeOmurYil = decimal.Round(donem.BakiyeOmur, 2),
            AktifDonemYil = decimal.Round(donem.AktifYil, 2),
            PasifDonemYil = decimal.Round(donem.PasifYil, 2),
            EsasAlinanAylikNetGelir = decimal.Round(aylikGelir, 2),
            ToplamPay = toplamPay,
            KusurOrani = kusur,
            HakSahipleri = dokum,
            BrutToplam = brutToplam,
            NetToplam = netToplam,
            Varsayimlar = varsayimlar,
            Kaynak = $"{Trh2010.Kaynak}; {MinimumWage.Kaynak}",
        })));
    }

    /// <summary>cocuklar dizisini okur; yoksa boş dizi, biçim bozuksa null.</summary>
    private static int[]? CocukYaslari(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("cocuklar", out var arr) || arr.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (arr.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var yaslar = new int[arr.GetArrayLength()];
        var i = 0;
        foreach (var item in arr.EnumerateArray())
        {
            if (!item.TryGetInt32(out var cy) || cy is < 0 or > 100)
            {
                return null;
            }

            yaslar[i++] = cy;
        }

        return yaslar;
    }
}

using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>2026 parametreleriyle brütten nete / netten brüte maaş hesaplar (asgari ücret istisnası dahil).</summary>
public sealed class BrutNetMaasTool : IAgentTool
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

    private static readonly string[] Varsayimlar =
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
        var yon = ToolArgs.String(arguments, "yon");
        var tutar = ToolArgs.Decimal(arguments, "tutar");
        if (yon is not ("brutten_nete" or "netten_brute") || tutar is not { } t || t <= 0)
        {
            return Task.FromResult(ToolResult.Error("yon (brutten_nete | netten_brute) ve pozitif tutar zorunludur."));
        }

        var ay = ToolArgs.Int(arguments, "ay") ?? 1;
        if (ay is < 1 or > 12)
        {
            return Task.FromResult(ToolResult.Error("ay 1 ile 12 arasında olmalıdır."));
        }

        var brut = yon == "brutten_nete" ? t : NettenBrute(t, ay);
        var b = Hesapla(brut, ay);

        decimal yillikNet = 0m;
        for (var m = 1; m <= 12; m++)
        {
            yillikNet += Hesapla(brut, m).Net;
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yon = yon,
            Ay = ay,
            BrutAylik = decimal.Round(b.Brut, 2),
            NetAylik = decimal.Round(b.Net, 2),
            Kesintiler = new
            {
                SgkIsci = b.Sgk,
                IssizlikIsci = b.Issizlik,
                GelirVergisi = b.GelirVergisi,
                DamgaVergisi = b.Damga,
                Toplam = b.Sgk + b.Issizlik + b.GelirVergisi + b.Damga,
            },
            AsgariUcretIstisnasi = new
            {
                GelirVergisi = b.GvIstisna,
                DamgaVergisi = b.DamgaIstisna,
            },
            YillikProjeksiyon = new
            {
                YillikBrut = decimal.Round(brut * 12m, 2),
                YillikNet = decimal.Round(yillikNet, 2),
                Aciklama = "Tüm yıl aynı brüt ücretle çalışıldığı varsayılarak 12 ayın kümülatif vergili net toplamı",
            },
            Varsayimlar,
            TarifeDonemi = "2026",
            Kaynak = PayrollParameters2026.Kaynak,
        })));
    }

    private sealed record Bordro(
        decimal Brut, decimal Sgk, decimal Issizlik, decimal GelirVergisi, decimal Damga,
        decimal GvIstisna, decimal DamgaIstisna, decimal Net);

    /// <summary>Ay konumuna göre tek ay bordrosu (kümülatif matrah = matrah x ay basitleştirmesi).</summary>
    private static Bordro Hesapla(decimal brut, int ay)
    {
        var sgkMatrah = Math.Min(brut, PayrollParameters2026.SgkTavanAylik);
        var sgk = decimal.Round(sgkMatrah * PayrollParameters2026.SgkIsciOrani, 2);
        var issizlik = decimal.Round(sgkMatrah * PayrollParameters2026.IssizlikIsciOrani, 2);
        var matrah = brut - sgk - issizlik;

        var gvTam = PayrollParameters2026.TarifeVergisi(matrah * ay)
            - PayrollParameters2026.TarifeVergisi(matrah * (ay - 1));

        // İstisna: asgari ücretlinin aynı ay konumunda ödeyeceği vergi kadar (GVK m.23/18).
        var istisnaMatrah = Math.Min(matrah, PayrollParameters2026.AsgariUcretBrut * 0.85m);
        var istisna = PayrollParameters2026.TarifeVergisi(istisnaMatrah * ay)
            - PayrollParameters2026.TarifeVergisi(istisnaMatrah * (ay - 1));
        var gelirVergisi = decimal.Round(Math.Max(0m, gvTam - istisna), 2);

        var damgaIstisnaMatrah = Math.Min(brut, PayrollParameters2026.AsgariUcretBrut);
        var damga = decimal.Round(
            Math.Max(0m, (brut - damgaIstisnaMatrah) * PayrollParameters2026.DamgaOrani), 2);

        var net = brut - sgk - issizlik - gelirVergisi - damga;
        return new Bordro(
            brut, sgk, issizlik, gelirVergisi, damga,
            decimal.Round(Math.Min(gvTam, istisna), 2),
            decimal.Round(damgaIstisnaMatrah * PayrollParameters2026.DamgaOrani, 2),
            net);
    }

    /// <summary>Hedef neti veren brütü ikili aramayla çözer (net, brüte göre artandır).</summary>
    private static decimal NettenBrute(decimal hedefNet, int ay)
    {
        var lo = hedefNet;
        var hi = hedefNet * 2m;
        for (var i = 0; i < 80; i++)
        {
            var mid = (lo + hi) / 2m;
            if (Hesapla(mid, ay).Net < hedefNet)
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

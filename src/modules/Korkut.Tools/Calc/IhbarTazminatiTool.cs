using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>İhbar tazminatı hesaplar (4857 sK m.17 süreleri, giydirilmiş brüt, gelir + damga vergisi).</summary>
public sealed class IhbarTazminatiTool : IAgentTool
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
        var brutMaas = ToolArgs.Decimal(arguments, "brut_maas");
        if (brutMaas is not { } maas || maas <= 0)
        {
            return Task.FromResult(ToolResult.Error("Pozitif brut_maas zorunludur."));
        }

        decimal kidemAy;
        if (ToolArgs.Decimal(arguments, "kidem_ay") is { } ay)
        {
            if (ay < 0)
            {
                return Task.FromResult(ToolResult.Error("kidem_ay negatif olamaz."));
            }

            kidemAy = ay;
        }
        else if (ToolArgs.Date(arguments, "ise_giris") is { } giris && ToolArgs.Date(arguments, "isten_cikis") is { } cikis)
        {
            if (giris >= cikis)
            {
                return Task.FromResult(ToolResult.Error("ise_giris, isten_cikis tarihinden önce olmalıdır."));
            }

            // 30 günlük ay yaklaşımı; süre basamağı belirlemek için yeterli hassasiyet.
            kidemAy = (cikis.DayNumber - giris.DayNumber) / 30m;
        }
        else
        {
            return Task.FromResult(ToolResult.Error("kidem_ay veya ise_giris + isten_cikis tarih çifti zorunludur."));
        }

        var yemek = ToolArgs.Decimal(arguments, "yemek") ?? 0m;
        var yol = ToolArgs.Decimal(arguments, "yol") ?? 0m;
        var diger = ToolArgs.Decimal(arguments, "diger") ?? 0m;
        if (yemek < 0 || yol < 0 || diger < 0)
        {
            return Task.FromResult(ToolResult.Error("yemek, yol ve diger negatif olamaz."));
        }

        var hafta = IhbarHaftasi(kidemAy);
        var giydirilmis = maas + yemek + yol + diger;
        var gunluk = giydirilmis / 30m;
        var brutTazminat = gunluk * 7m * hafta;

        // Gelir vergisi yaklaşık: tazminat tutarı 2026 ücret tarifesine tek başına uygulanır.
        // Gerçek kesinti, işçinin yıl içi kümülatif vergi matrahına göre değişir.
        var gelirVergisi = decimal.Round(PayrollParameters2026.TarifeVergisi(brutTazminat), 2);
        var damga = decimal.Round(brutTazminat * PayrollParameters2026.DamgaOrani, 2);
        var brut = decimal.Round(brutTazminat, 2);

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            KidemAy = decimal.Round(kidemAy, 2),
            IhbarSuresiHafta = hafta,
            GiydirilmisBrut = decimal.Round(giydirilmis, 2),
            GunlukUcret = decimal.Round(gunluk, 2),
            BrutIhbarTazminati = brut,
            GelirVergisiYaklasik = gelirVergisi,
            DamgaVergisi = damga,
            NetIhbarTazminatiYaklasik = brut - gelirVergisi - damga,
            Not = "İhbar tazminatı gelir vergisine ve binde 7,59 damga vergisine tabidir, SGK primi kesilmez. Gelir vergisi burada tazminat tutarının 2026 ücret tarifesine tek başına uygulanmasıyla yaklaşık hesaplanmıştır; gerçek kesinti işçinin yıl içi kümülatif matrahındaki dilime göre değişir.",
            Kaynak = "4857 sK m.17; GVK m.61 ve m.103 (2026 tarifesi); 488 sK IV/1-b (binde 7,59)",
        })));
    }

    /// <summary>4857 m.17: 6 aydan az 2; 6 aydan 1,5 yıla kadar 4; 1,5 yıldan 3 yıla kadar 6; 3 yıldan fazla 8 hafta.</summary>
    private static int IhbarHaftasi(decimal kidemAy) => kidemAy switch
    {
        < 6m => 2,
        < 18m => 4,
        <= 36m => 6,
        _ => 8,
    };
}

using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Serbest meslek makbuzu hesaplar: brüt/net/KDV dahil tutardan stopaj, KDV ve tahsil edilecek toplamı çıkarır.</summary>
public sealed class SmmTool : IAgentTool
{
    private const string Kaynak =
        "GVK m.94/2-b (serbest meslek stopajı %20); KDV genel oranı %20 (7346 CB Kararı); avukatlık hizmetlerinde 5/10 KDV tevkifatı: KDV Genel Uygulama Tebliği I/C-2.1.3.2.13";

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "yon": {
              "type": "string",
              "enum": ["brutten", "netten", "kdv_dahilden"],
              "description": "Verilen tutarın anlamı: brüt ücret, net ele geçen veya KDV dahil toplam"
            },
            "tutar": { "type": "number", "description": "TL" },
            "kdv_orani": { "type": "number", "description": "Varsayılan 20" },
            "stopaj_orani": { "type": "number", "description": "Varsayılan 20" },
            "kdv_tevkifati": {
              "type": "boolean",
              "description": "5/10 KDV tevkifatı uygulansın mı (tevkifat yükümlüsü alıcılara verilen avukatlık hizmeti)"
            }
          },
          "required": ["yon", "tutar"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_smm",
        "Serbest meslek makbuzu hesaplar: brütten, netten veya KDV dahil tutardan brüt ücret, gelir vergisi stopajı, net ele geçen, KDV (istenirse 5/10 tevkifatlı) ve tahsil edilecek toplamı çıkarır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var yon = ToolArgs.String(arguments, "yon");
        var tutar = ToolArgs.Decimal(arguments, "tutar");
        if (yon is null || tutar is not { } t || t <= 0)
        {
            return Task.FromResult(ToolResult.Error("yon ve pozitif tutar zorunludur."));
        }

        var kdvOrani = ToolArgs.Decimal(arguments, "kdv_orani") ?? 20m;
        var stopajOrani = ToolArgs.Decimal(arguments, "stopaj_orani") ?? 20m;
        if (kdvOrani is < 0 or > 100 || stopajOrani is < 0 or >= 100)
        {
            return Task.FromResult(ToolResult.Error("kdv_orani 0-100, stopaj_orani 0-99 aralığında olmalıdır."));
        }

        var kdv = kdvOrani / 100m;
        var stopaj = stopajOrani / 100m;
        decimal brut;
        switch (yon)
        {
            case "brutten":
                brut = t;
                break;
            case "netten":
                brut = t / (1m - stopaj);
                break;
            case "kdv_dahilden":
                brut = t / (1m + kdv);
                break;
            default:
                return Task.FromResult(ToolResult.Error("yon brutten, netten veya kdv_dahilden olmalıdır."));
        }

        var tevkifatli = ToolArgs.Bool(arguments, "kdv_tevkifati");
        var stopajTutari = brut * stopaj;
        var kdvTutari = brut * kdv;
        var kdvTevkifati = tevkifatli ? kdvTutari * 0.5m : 0m;
        var net = brut - stopajTutari;
        var tahsil = net + kdvTutari - kdvTevkifati;

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Brut = decimal.Round(brut, 2),
            StopajOrani = stopajOrani,
            Stopaj = decimal.Round(stopajTutari, 2),
            NetEleGecen = decimal.Round(net, 2),
            KdvOrani = kdvOrani,
            Kdv = decimal.Round(kdvTutari, 2),
            KdvTevkifati = decimal.Round(kdvTevkifati, 2),
            TahsilEdilecekToplam = decimal.Round(tahsil, 2),
            Not = "Stopaj yalnızca GVK m.94 kapsamındaki vergi sorumlusu ödeyicilerde (şirket, kurum vb.) kesilir; nihai tüketiciye makbuzda stopaj uygulanmaz. 5/10 KDV tevkifatı yalnızca tevkifat yükümlüsü alıcılarda geçerlidir.",
            Kaynak,
        })));
    }
}

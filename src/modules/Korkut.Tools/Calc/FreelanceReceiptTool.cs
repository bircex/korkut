using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Serbest meslek makbuzu hesaplar: brüt/net/KDV dahil tutardan stopaj, KDV ve tahsil edilecek toplamı çıkarır.</summary>
public sealed class FreelanceReceiptTool : IAgentTool
{
    private const string Source =
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
        var direction = ToolArgs.String(arguments, "yon");
        var amount = ToolArgs.Decimal(arguments, "tutar");
        if (direction is null || amount is not { } value || value <= 0)
        {
            return Task.FromResult(ToolResult.Error("yon ve pozitif tutar zorunludur."));
        }

        var vatRate = ToolArgs.Decimal(arguments, "kdv_orani") ?? 20m;
        var withholdingRate = ToolArgs.Decimal(arguments, "stopaj_orani") ?? 20m;
        if (vatRate is < 0 or > 100 || withholdingRate is < 0 or >= 100)
        {
            return Task.FromResult(ToolResult.Error("kdv_orani 0-100, stopaj_orani 0-99 aralığında olmalıdır."));
        }

        var vatFraction = vatRate / 100m;
        var withholdingFraction = withholdingRate / 100m;
        decimal gross;
        switch (direction)
        {
            case "brutten":
                gross = value;
                break;
            case "netten":
                gross = value / (1m - withholdingFraction);
                break;
            case "kdv_dahilden":
                gross = value / (1m + vatFraction);
                break;
            default:
                return Task.FromResult(ToolResult.Error("yon brutten, netten veya kdv_dahilden olmalıdır."));
        }

        var reverseCharge = ToolArgs.Bool(arguments, "kdv_tevkifati");
        var withholdingAmount = gross * withholdingFraction;
        var vatAmount = gross * vatFraction;
        var vatReverseChargeAmount = reverseCharge ? vatAmount * 0.5m : 0m;
        var net = gross - withholdingAmount;
        var totalCollected = net + vatAmount - vatReverseChargeAmount;

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Brut = decimal.Round(gross, 2),
            StopajOrani = withholdingRate,
            Stopaj = decimal.Round(withholdingAmount, 2),
            NetEleGecen = decimal.Round(net, 2),
            KdvOrani = vatRate,
            Kdv = decimal.Round(vatAmount, 2),
            KdvTevkifati = decimal.Round(vatReverseChargeAmount, 2),
            TahsilEdilecekToplam = decimal.Round(totalCollected, 2),
            Not = "Stopaj yalnızca GVK m.94 kapsamındaki vergi sorumlusu ödeyicilerde (şirket, kurum vb.) kesilir; nihai tüketiciye makbuzda stopaj uygulanmaz. 5/10 KDV tevkifatı yalnızca tevkifat yükümlüsü alıcılarda geçerlidir.",
            Kaynak = Source,
        })));
    }
}

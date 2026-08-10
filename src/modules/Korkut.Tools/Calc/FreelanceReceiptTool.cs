using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Serbest meslek makbuzu hesaplar: brüt/net/KDV dahil tutardan stopaj, KDV ve tahsil edilecek toplamı çıkarır. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class FreelanceReceiptTool(ICalculatorService calc) : IAgentTool
{
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
        var request = new FreelanceReceiptRequest(
            ToolArgs.String(arguments, "yon") ?? "",
            ToolArgs.Decimal(arguments, "tutar") ?? -1m,
            ToolArgs.Decimal(arguments, "kdv_orani"),
            ToolArgs.Decimal(arguments, "stopaj_orani"),
            ToolArgs.Bool(arguments, "kdv_tevkifati"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcFreelanceReceipt(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

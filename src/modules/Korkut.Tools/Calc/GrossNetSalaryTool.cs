using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>2026 parametreleriyle brütten nete / netten brüte maaş hesaplar (asgari ücret istisnası dahil). Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class GrossNetSalaryTool(ICalculatorService calc) : IAgentTool
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

    public ToolSpec Spec => new(
        "calc_brut_net_maas",
        "2026 bordro parametreleriyle brütten nete veya netten brüte maaş hesaplar: SGK işçi %14 + işsizlik %1 (tavan 247.725 TL), 2026 gelir vergisi ücret tarifesi, damga binde 7,59 ve asgari ücret istisnası. Netten brüte iteratif çözülür; yıllık projeksiyon da verilir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new GrossNetSalaryRequest(
            ToolArgs.String(arguments, "yon") ?? "",
            ToolArgs.Decimal(arguments, "tutar") ?? -1m,
            ToolArgs.Int(arguments, "ay"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcGrossNetSalary(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

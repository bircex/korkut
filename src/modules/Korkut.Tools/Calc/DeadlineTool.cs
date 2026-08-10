using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Usul süresi hesabı: kanun yolu ve cevap sürelerinin son gününü tatil kurallarıyla birlikte bulur. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class DeadlineTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "sure_turu": {
              "type": "string",
              "enum": ["istinaf_hukuk", "istinaf_ceza", "temyiz_hukuk", "temyiz_ceza", "cevap_dilekcesi", "icra_itiraz", "itirazin_iptali", "istinaf_idare", "temyiz_idare"],
              "description": "Hesaplanacak sürenin türü"
            },
            "teblig_tarihi": { "type": "string", "format": "date", "description": "Kararın/ödeme emrinin/dava dilekçesinin tebliğ tarihi" }
          },
          "required": ["sure_turu", "teblig_tarihi"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_sure",
        "Usul sürelerinin son gününü hesaplar: hukuk/ceza/idare istinaf ve temyiz, cevap dilekçesi, icra takibine itiraz ve itirazın iptali. Süre tebliğin ertesi günü başlar; son günün hafta sonu veya resmi tatile denk gelmesi ve adli tatil (HMK m.104) / çalışmaya ara verme (İYUK m.8/3) uzamaları uygulanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new DeadlineRequest(
            ToolArgs.String(arguments, "sure_turu") ?? "",
            ToolArgs.Date(arguments, "teblig_tarihi"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcDeadline(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

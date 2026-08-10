using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Belli parayı ihtiva eden kağıtların damga vergisini (488 sK, 2026 azami sınırıyla) hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class StampDutyTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "kagit_turu": {
              "type": "string",
              "enum": ["sozlesme", "kira_sozlesmesi", "ihale_karari", "ucret_odemesi", "fesihname"],
              "description": "sozlesme = mukavelename/taahhütname/temlikname (binde 9,48); kira_sozlesmesi binde 1,89; ihale_karari binde 5,69; ucret_odemesi binde 7,59; fesihname binde 1,89"
            },
            "tutar": { "type": "number", "description": "Kağıdın ihtiva ettiği tutar (TL); kira sözleşmesinde sözleşme süresi toplam kira bedeli" }
          },
          "required": ["kagit_turu", "tutar"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_damga_vergisi",
        "488 sayılı Kanuna göre damga vergisi hesaplar: kağıt türüne göre binde oran uygular, 2026 azami damga vergisi üst sınırını gözetir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new StampDutyRequest(
            ToolArgs.String(arguments, "kagit_turu") ?? "",
            ToolArgs.Decimal(arguments, "tutar") ?? -1m);

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcStampDuty(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

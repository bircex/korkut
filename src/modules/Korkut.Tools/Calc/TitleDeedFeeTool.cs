using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Gayrimenkul satışında tapu harcını (alıcı + satıcı, binde 20 + binde 20) hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class TitleDeedFeeTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "satis_bedeli": { "type": "number", "description": "Tapuda beyan edilen satış bedeli (TL); emlak vergisi değerinden düşük olamaz" },
            "taraf": {
              "type": "string",
              "enum": ["alici", "satici", "toplam"],
              "description": "Hangi tarafın harcı istendiği (varsayılan toplam)"
            }
          },
          "required": ["satis_bedeli"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_tapu_harci",
        "Gayrimenkul satışında tapu harcını hesaplar (492 sK 4 sayılı tarife I-20/a): alıcı ve satıcıdan ayrı ayrı binde 20; döner sermaye ücreti hakkında not içerir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new TitleDeedFeeRequest(
            ToolArgs.Decimal(arguments, "satis_bedeli") ?? -1m,
            ToolArgs.String(arguments, "taraf"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcTitleDeedFee(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

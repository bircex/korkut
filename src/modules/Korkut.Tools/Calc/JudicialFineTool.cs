using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Adli para cezası hesabı: gün sayısı x günlük miktar, hapse çevirme karşılığı ve taksit kuralı. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class JudicialFineTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "gun_sayisi": { "type": "integer", "description": "Hükmedilen tam gün sayısı (TCK m.52/1: 5-730 gün)" },
            "gunluk_miktar": { "type": "number", "description": "Bir gün karşılığı miktar, TL (TCK m.52/2: güncel aralık 100-500 TL)" },
            "taksit": { "type": "boolean", "description": "Taksitle ödeme bilgisi de istenirse true (TCK m.52/4)" }
          },
          "required": ["gun_sayisi", "gunluk_miktar"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_adli_para_cezasi",
        "Adli para cezasını hesaplar: gün sayısı x günlük miktar = toplam ceza, ödenmemesi halinde hapse çevirme karşılığı (gün) ve istenirse taksit kuralı (TCK m.52, 5275 m.106).",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new JudicialFineRequest(
            ToolArgs.Int(arguments, "gun_sayisi") ?? -1,
            ToolArgs.Decimal(arguments, "gunluk_miktar") ?? -1m,
            ToolArgs.Bool(arguments, "taksit"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcJudicialFine(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>İşe iade davası parasal sonuçlarını hesaplar (4857 sK m.21: boşta geçen süre + işe başlatmama tazminatı). Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class ReinstatementPayTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "aylik_brut": { "type": "number", "description": "Aylık brüt ücret, TL" },
            "kidem_yil": { "type": "number", "description": "Kıdem süresi, yıl olarak (örn. 3.5)" }
          },
          "required": ["aylik_brut", "kidem_yil"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_ise_iade_tazminati",
        "İşe iade davasının parasal sonuçlarını hesaplar: boşta geçen süre ücreti (en çok 4 aya kadar) ve işe başlatmama tazminatı (4-8 aylık ücret, kıdeme göre mahkeme takdiri; pratik bant verilir). Sonuçlar tahminidir, mahkeme takdirine tabidir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new ReinstatementPayRequest(
            ToolArgs.Decimal(arguments, "aylik_brut") ?? -1m,
            ToolArgs.Decimal(arguments, "kidem_yil") ?? -1m);

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcReinstatementPay(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

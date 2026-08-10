using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Kıdem tazminatı hesaplar (1475 sK m.14: giydirilmiş brüt, tavan kontrolü, damga kesintisi). Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class SeveranceTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "ise_giris": { "type": "string", "format": "date", "description": "İşe giriş tarihi" },
            "isten_cikis": { "type": "string", "format": "date", "description": "İş sözleşmesinin sona erdiği tarih (fesih tarihi)" },
            "brut_maas": { "type": "number", "description": "Son aylık brüt çıplak ücret, TL" },
            "yemek": { "type": "number", "description": "Aylık brüt yemek yardımı, TL (opsiyonel)" },
            "yol": { "type": "number", "description": "Aylık brüt yol yardımı, TL (opsiyonel)" },
            "diger": { "type": "number", "description": "Diğer düzenli aylık brüt yan haklar (prim, ikramiye vb.), TL (opsiyonel)" }
          },
          "required": ["ise_giris", "isten_cikis", "brut_maas"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_kidem_tazminati",
        "Kıdem tazminatı hesaplar: giydirilmiş brüt ücret (çıplak brüt + yemek + yol + diğer yan haklar), fesih tarihindeki kıdem tavanı kontrolü, tam yıl + kalan gün orantısı ve damga vergisi kesintisi (binde 7,59). Kıdem tazminatından gelir vergisi ve SGK primi kesilmez.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new SeveranceRequest(
            ToolArgs.Date(arguments, "ise_giris"),
            ToolArgs.Date(arguments, "isten_cikis"),
            ToolArgs.Decimal(arguments, "brut_maas") ?? -1m,
            ToolArgs.Decimal(arguments, "yemek"),
            ToolArgs.Decimal(arguments, "yol"),
            ToolArgs.Decimal(arguments, "diger"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcSeverance(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

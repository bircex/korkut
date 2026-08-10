using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// Ölümlü olayda destekten yoksun kalma tazminatını mahkeme modunda hesaplar. Thin adapter over
/// <see cref="ICalculatorService"/>.
/// </summary>
public sealed class LossOfSupportTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "cinsiyet": { "type": "string", "enum": ["erkek", "kadin"], "description": "Müteveffanın cinsiyeti (TRH-2010 bakiye ömrü için)" },
            "yas": { "type": "integer", "minimum": 0, "maximum": 100, "description": "Müteveffanın ölüm tarihindeki yaşı (0-100)" },
            "aylik_net_gelir": { "type": "number", "description": "Müteveffanın aylık net geliri (TL); verilmezse AGİ hariç net asgari ücret alınır (2026: 28.075,50 TL)" },
            "kusur_orani": { "type": "number", "minimum": 0, "maximum": 100, "description": "MÜTEVEFFANIN kendi kusuru, yüzde (0-100); verilmezse 0 alınır" },
            "es_var": { "type": "boolean", "description": "Sağ kalan eş var mı" },
            "es_yasi": { "type": "integer", "description": "Eşin yaşı; verilmezse müteveffanın yaşıyla aynı varsayılır" },
            "cocuklar": { "type": "array", "items": { "type": "integer" }, "description": "Çocukların yaşları (destek 18 yaşına kadar)" },
            "anne_var": { "type": "boolean", "description": "Destek gören anne var mı" },
            "anne_yasi": { "type": "integer", "description": "Annenin yaşı; verilmezse müteveffanın yaşı + 25 varsayılır" },
            "baba_var": { "type": "boolean", "description": "Destek gören baba var mı" },
            "baba_yasi": { "type": "integer", "description": "Babanın yaşı; verilmezse müteveffanın yaşı + 25 varsayılır" }
          },
          "required": ["cinsiyet", "yas"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_destekten_yoksunluk",
        "Ölümlü olayda (trafik kazası vb.) destekten yoksun kalma tazminatını mahkeme modunda hak sahibi bazında hesaplar: TRH-2010 bakiye ömür, progresif rant (net teknik faiz %0), pay dağılımı müteveffa 2 / eş 2 / çocuk 1 / anne-baba 1'er; müteveffa kusuru düşülür.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        List<int>? childAges = null;
        if (arguments.TryGetProperty("cocuklar", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            childAges = arr.EnumerateArray()
                .Select(e => e.TryGetInt32(out var age) ? age : (int?)null)
                .Where(a => a.HasValue)
                .Select(a => a!.Value)
                .ToList();
        }

        var request = new LossOfSupportRequest(
            ToolArgs.String(arguments, "cinsiyet") ?? "",
            ToolArgs.Int(arguments, "yas") ?? -1,
            ToolArgs.Decimal(arguments, "aylik_net_gelir"),
            ToolArgs.Decimal(arguments, "kusur_orani"),
            ToolArgs.Bool(arguments, "es_var"),
            ToolArgs.Int(arguments, "es_yasi"),
            childAges,
            ToolArgs.Bool(arguments, "anne_var"),
            ToolArgs.Int(arguments, "anne_yasi"),
            ToolArgs.Bool(arguments, "baba_var"),
            ToolArgs.Int(arguments, "baba_yasi"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcLossOfSupport(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

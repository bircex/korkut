using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// İş kazasında işçinin sürekli/geçici iş göremezlik (maddi) tazminatını mahkeme modunda hesaplar.
/// Thin adapter over <see cref="ICalculatorService"/>.
/// </summary>
public sealed class WorkAccidentCompensationTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "cinsiyet": { "type": "string", "enum": ["erkek", "kadin"], "description": "İşçinin cinsiyeti (TRH-2010 bakiye ömrü için)" },
            "yas": { "type": "integer", "minimum": 0, "maximum": 100, "description": "Kaza tarihindeki yaş (0-100)" },
            "surekli_is_goremezlik_orani": { "type": "number", "minimum": 0, "maximum": 100, "description": "Sürekli iş göremezlik (maluliyet) oranı, yüzde (0-100)" },
            "isveren_kusur_orani": { "type": "number", "minimum": 0, "maximum": 100, "description": "İşverenin kusur oranı, yüzde (0-100); tazminat bu oranla çarpılır. Verilmezse 100 alınır" },
            "aylik_net_gelir": { "type": "number", "description": "İşçinin aylık net ücreti (TL); verilmezse AGİ hariç net asgari ücret alınır (2026: 28.075,50 TL)" },
            "gecici_is_goremezlik_ay": { "type": "number", "description": "Geçici iş göremezlik (istirahat) süresi, ay; bu dönem %100 iş göremezlikle hesaplanır" },
            "sgk_gelir_baglandi": { "type": "boolean", "description": "SGK sürekli iş göremezlik geliri bağladı mı" },
            "sgk_pesin_sermaye": { "type": "number", "description": "SGK gelirinin peşin sermaye değeri (TL); verilirse tazminattan düşülür" }
          },
          "required": ["cinsiyet", "yas", "surekli_is_goremezlik_orani"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_is_kazasi_tazminati",
        "İş kazasında işçinin maddi (sürekli/geçici iş göremezlik) tazminatını mahkeme modunda hesaplar: TRH-2010 bakiye ömür, progresif rant (net teknik faiz %0), aktif dönem 60 yaşa kadar, pasif dönem AGİ hariç net asgari ücret; tazminat işverenin kusur oranıyla çarpılır, SGK peşin sermaye değeri verilirse düşülür.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new WorkAccidentCompensationRequest(
            ToolArgs.String(arguments, "cinsiyet") ?? "",
            ToolArgs.Int(arguments, "yas") ?? -1,
            ToolArgs.Decimal(arguments, "surekli_is_goremezlik_orani") ?? -1m,
            ToolArgs.Decimal(arguments, "isveren_kusur_orani"),
            ToolArgs.Decimal(arguments, "aylik_net_gelir"),
            ToolArgs.Decimal(arguments, "gecici_is_goremezlik_ay"),
            ToolArgs.Bool(arguments, "sgk_gelir_baglandi"),
            ToolArgs.Decimal(arguments, "sgk_pesin_sermaye"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcWorkAccidentCompensation(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

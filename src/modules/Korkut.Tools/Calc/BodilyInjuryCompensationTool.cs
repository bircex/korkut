using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// Yaralanmalı trafik kazasında sürekli/geçici iş göremezlik (bedensel) tazminatını mahkeme
/// modunda hesaplar. Thin adapter over <see cref="ICalculatorService"/>.
/// </summary>
public sealed class BodilyInjuryCompensationTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "cinsiyet": { "type": "string", "enum": ["erkek", "kadin"], "description": "Mağdurun cinsiyeti (TRH-2010 bakiye ömrü için)" },
            "yas": { "type": "integer", "description": "Kaza tarihindeki yaş (0-100)" },
            "maluliyet_orani": { "type": "number", "description": "Sürekli maluliyet (iş göremezlik) oranı, yüzde (0-100)" },
            "kusur_orani": { "type": "number", "description": "MAĞDURUN kendi kusuru, yüzde (0-100); karşı tarafın kusuru DEĞİL. Verilmezse 0 alınır" },
            "aylik_net_gelir": { "type": "number", "description": "Mağdurun aylık net geliri (TL); verilmezse AGİ hariç net asgari ücret alınır (2026: 28.075,50 TL)" },
            "gecici_is_goremezlik_ay": { "type": "number", "description": "Geçici iş göremezlik (iyileşme) süresi, ay; bu dönem %100 iş göremezlikle hesaplanır" }
          },
          "required": ["cinsiyet", "yas", "maluliyet_orani"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_bedensel_tazminat",
        "Yaralanmalı trafik kazasında bedensel (sürekli/geçici iş göremezlik) tazminatını mahkeme modunda hesaplar: TRH-2010 bakiye ömür, progresif rant (net teknik faiz %0), aktif dönem 60 yaşa kadar, pasif dönem AGİ hariç net asgari ücretten; mağdur kusuru düşülür.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new BodilyInjuryCompensationRequest(
            ToolArgs.String(arguments, "cinsiyet") ?? "",
            ToolArgs.Int(arguments, "yas") ?? -1,
            ToolArgs.Decimal(arguments, "maluliyet_orani") ?? -1m,
            ToolArgs.Decimal(arguments, "kusur_orani"),
            ToolArgs.Decimal(arguments, "aylik_net_gelir"),
            ToolArgs.Decimal(arguments, "gecici_is_goremezlik_ay"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcBodilyInjuryCompensation(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

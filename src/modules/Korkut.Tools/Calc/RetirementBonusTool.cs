using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>5434 sK m.89 emekli ikramiyesi (EKEAT x tam hizmet yılı) hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class RetirementBonusTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "ekeat_brut": { "type": "number", "description": "TL; emekli keseneğine esas aylık tutarı (bordro/e-Devlet hizmet dökümünden alınır, brüt maaş DEĞİLDİR)" },
            "hizmet_yili": { "type": "number", "description": "Toplam hizmet süresi, yıl cinsinden (kesirli olabilir; yalnızca tam yıllar ikramiyeye dahil edilir)" }
          },
          "required": ["ekeat_brut", "hizmet_yili"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_emeklilik_ikramiyesi",
        "5434 sK m.89'a göre memur emekli ikramiyesini hesaplar: EKEAT x tam hizmet yılı sayısı. 30 yıllık tavan AYM kararıyla (2014) kaldırıldığından tavan uygulanmaz.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new RetirementBonusRequest(
            ToolArgs.Decimal(arguments, "ekeat_brut") ?? -1m,
            ToolArgs.Decimal(arguments, "hizmet_yili") ?? -1m);

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcRetirementBonus(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

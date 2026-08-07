using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>4447 sK m.50-51 işsizlik ödeneği hak kazanma ve tutar hesabı. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class UnemploymentBenefitTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "son_120_gun_kesintisiz_calisti": { "type": "boolean", "description": "İşten ayrılmadan önceki son 120 gün hizmet akdine tabi kesintisiz çalışıldı mı" },
            "son_3_yilda_prim_gunu": { "type": "integer", "description": "Son 3 yılda ödenen toplam işsizlik sigortası prim günü" },
            "son_4_ay_toplam_prime_esas_kazanc": { "type": "number", "description": "TL; işten ayrılmadan önceki son 4 aylık toplam prime esas kazanç" }
          },
          "required": ["son_120_gun_kesintisiz_calisti", "son_3_yilda_prim_gunu", "son_4_ay_toplam_prime_esas_kazanc"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_issizlik_odenegi",
        "4447 sK m.50-51'e göre işsizlik ödeneği hak kazanma şartını, ödenek süresini ve günlük/aylık brüt-net tutarını hesaplar (son 4 ay ortalama kazancın %40'ı, brüt asgari ücretin %80'i tavanıyla sınırlı).",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new UnemploymentBenefitRequest(
            ToolArgs.Bool(arguments, "son_120_gun_kesintisiz_calisti"),
            ToolArgs.Int(arguments, "son_3_yilda_prim_gunu") ?? -1,
            ToolArgs.Decimal(arguments, "son_4_ay_toplam_prime_esas_kazanc") ?? -1m);

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcUnemploymentBenefit(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

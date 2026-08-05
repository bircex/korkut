using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// 6183 sayılı AATUHK m.51 gecikme zammı hesaplar (vergi, SGK primi ve diğer amme alacakları).
/// Thin adapter over <see cref="ICalculatorService"/>.
/// </summary>
public sealed class LatePaymentSurchargeTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "ana_para": { "type": "number", "description": "Amme alacağının aslı, TL" },
            "vade_tarihi": { "type": "string", "format": "date", "description": "Borcun vade (son ödeme) tarihi" },
            "odeme_tarihi": { "type": "string", "format": "date", "description": "Fiili ödeme veya hesap tarihi" }
          },
          "required": ["ana_para", "vade_tarihi", "odeme_tarihi"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_gecikme_zammi",
        "6183 sayılı AATUHK m.51 gecikme zammı hesaplar (vergi, SGK ve diğer amme alacakları için; özel hukuk "
        + "alacaklarında calc_faiz kullanın). Aylık oran dönem tablosuna göre parçalı; ay kesirleri günlük hesaplanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new LatePaymentSurchargeRequest(
            ToolArgs.Decimal(arguments, "ana_para") ?? -1m,
            ToolArgs.Date(arguments, "vade_tarihi"),
            ToolArgs.Date(arguments, "odeme_tarihi"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcLatePaymentSurcharge(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// Kazalı aracın değer kaybını ZMSS Genel Şartları ekindeki resmi formülle hesaplar. Thin adapter
/// over <see cref="ICalculatorService"/>.
/// </summary>
public sealed class VehicleValueLossTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "piyasa_degeri": { "type": "number", "description": "Aracın kaza tarihindeki hasarsız rayiç (piyasa) değeri, TL" },
            "kilometre": { "type": "integer", "description": "Aracın kaza tarihindeki kilometresi" },
            "hasar_boyutu": { "type": "string", "enum": ["buyuk", "orta", "kucuk", "basit"], "description": "Hasar boyutu (Genel Şartlar A1-A4). Verilmezse hasar_tutari'ndan türetilir" },
            "hasar_tutari": { "type": "number", "description": "Onarım (hasar) tutarı, TL; hasar_boyutu verilmezse boyut bundan türetilir (pratik yaklaşım)" }
          },
          "required": ["piyasa_degeri", "kilometre"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_arac_deger_kaybi",
        "Kazalı aracın değer kaybını ZMSS Genel Şartları ekindeki resmi formülle hesaplar: baz değer kaybı (rayiç x %19) x hasar boyutu katsayısı (büyük 0,90 / orta 0,75 / küçük 0,50 / basit 0,25) x kilometre katsayısı. Hasar boyutu verilmezse onarım tutarından türetilir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new VehicleValueLossRequest(
            ToolArgs.Decimal(arguments, "piyasa_degeri") ?? -1m,
            ToolArgs.Int(arguments, "kilometre") ?? -1,
            ToolArgs.String(arguments, "hasar_boyutu"),
            ToolArgs.Decimal(arguments, "hasar_tutari"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcVehicleValueLoss(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

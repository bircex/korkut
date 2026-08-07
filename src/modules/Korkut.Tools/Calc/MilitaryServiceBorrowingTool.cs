using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>5510 sK m.41 askerlik borçlanması tutarını hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class MilitaryServiceBorrowingTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "gun_sayisi": { "type": "integer", "description": "Borçlanılacak fiili askerlik günü sayısı" },
            "gunluk_tutar": { "type": "number", "description": "İsteğe bağlı; TL, günlük alt sınır (asgari ücret) ile üst sınır (alt sınırın 9 katı) arasında. Verilmezse alt sınır kullanılır" }
          },
          "required": ["gun_sayisi"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_askerlik_borclanmasi",
        "5510 sK m.41'e göre askerlik borçlanması tutarını hesaplar: gün sayısı x seçilen günlük kazanç x %45 prim oranı (7566 sK m.21 ile 2026'da değişti).",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new MilitaryServiceBorrowingRequest(
            ToolArgs.Int(arguments, "gun_sayisi") ?? -1,
            ToolArgs.Decimal(arguments, "gunluk_tutar"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcMilitaryServiceBorrowing(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>3201 sK yurtdışı hizmet borçlanması tutarını hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class OverseasBorrowingTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "gun_sayisi": { "type": "integer", "description": "Borçlanılacak yurtdışında geçen gün sayısı" },
            "gunluk_tutar": { "type": "number", "description": "İsteğe bağlı; TL, günlük alt sınır (asgari ücret) ile üst sınır (alt sınırın 9 katı) arasında. Verilmezse alt sınır kullanılır" }
          },
          "required": ["gun_sayisi"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_yurtdisi_borclanma",
        "3201 sK'ya göre yurtdışında geçen sürelerin (çalışma, ev kadınlığı vb.) borçlanma tutarını hesaplar: gün sayısı x seçilen günlük kazanç x %45 prim oranı (2008'den beri sabit).",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new OverseasBorrowingRequest(
            ToolArgs.Int(arguments, "gun_sayisi") ?? -1,
            ToolArgs.Decimal(arguments, "gunluk_tutar"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcOverseasBorrowing(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

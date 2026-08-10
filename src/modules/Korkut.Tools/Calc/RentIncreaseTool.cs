using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// TBK m.344 kira artışı üst sınırını hesaplar: yenilenen kira döneminde artış, bir önceki kira
/// yılına ait TÜFE on iki aylık ortalamalara göre değişim oranını geçemez. Thin adapter over
/// <see cref="ICalculatorService"/>.
/// </summary>
public sealed class RentIncreaseTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "mevcut_kira": { "type": "number", "description": "Mevcut aylık kira bedeli, TL" },
            "yenileme_ayi": { "type": "string", "pattern": "^\\d{4}-\\d{2}$", "description": "Kira döneminin yenilendiği ay, YYYY-MM (örn. 2026-07)" },
            "tufe_orani": { "type": "number", "description": "İsteğe bağlı: uygulanacak 12 aylık ortalama TÜFE (%). Verilmezse gömülü TÜİK tablosundan bulunur" }
          },
          "required": ["mevcut_kira", "yenileme_ayi"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_kira_artis",
        "TBK m.344 kira artışı üst sınırını hesaplar: azami artış, yenileme ayından bir önceki ayın TÜİK 12 aylık "
        + "ortalama TÜFE değişim oranıdır. Konut ve çatılı işyeri kiralarının ikisinde de bu sınır uygulanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new RentIncreaseRequest(
            ToolArgs.Decimal(arguments, "mevcut_kira") ?? -1m,
            ToolArgs.String(arguments, "yenileme_ayi") ?? "",
            ToolArgs.Decimal(arguments, "tufe_orani"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcRentIncrease(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

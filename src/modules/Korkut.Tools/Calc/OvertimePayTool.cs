using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Fazla mesai, hafta tatili ve resmi tatil çalışma ücretlerini hesaplar (4857 sK m.41, 46, 47). Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class OvertimePayTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "aylik_brut_ucret": { "type": "number", "description": "Aylık brüt ücret, TL" },
            "fazla_mesai_saat": { "type": "number", "description": "Normal fazla mesai saati (haftalık 45 saati aşan çalışma)" },
            "hafta_tatili_saat": { "type": "number", "description": "Hafta tatilinde çalışılan saat" },
            "resmi_tatil_gun": { "type": "number", "description": "Ulusal bayram ve genel tatilde çalışılan gün sayısı" }
          },
          "required": ["aylik_brut_ucret"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_fazla_mesai",
        "Fazla mesai (x1,5), hafta tatili çalışması (x2,5) ve ulusal bayram/genel tatil çalışması (günlük ücret x2) brüt alacaklarını hesaplar. Saatlik ücret aylık brüt / 225 kabulüyle bulunur; çıktı brüttür, vergi ve SGK kesintileri ayrıca uygulanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new OvertimePayRequest(
            ToolArgs.Decimal(arguments, "aylik_brut_ucret") ?? -1m,
            ToolArgs.Decimal(arguments, "fazla_mesai_saat"),
            ToolArgs.Decimal(arguments, "hafta_tatili_saat"),
            ToolArgs.Decimal(arguments, "resmi_tatil_gun"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcOvertimePay(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

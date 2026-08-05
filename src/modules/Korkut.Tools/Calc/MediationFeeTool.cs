using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>2026 Arabuluculuk Asgari Ücret Tarifesi'ne göre saatlik veya anlaşma (nispi) ücretini hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class MediationFeeTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "uyusmazlik_turu": {
              "type": "string",
              "enum": ["aile", "isci_isveren", "tuketici", "kira", "ortakligin_giderilmesi", "ticari", "tarimsal", "diger"],
              "description": "kira = kira, komşu hakkı ve kat mülkiyeti uyuşmazlıkları; tarimsal = tarımsal üretim sözleşmelerinden kaynaklanan uyuşmazlıklar"
            },
            "anlasma": { "type": "boolean", "description": "Süreç anlaşmayla mı sonuçlandı" },
            "anlasma_tutari": { "type": "number", "description": "TL; anlaşma varsa üzerinde anlaşılan tutar" },
            "taraf_sayisi": { "type": "integer", "description": "Varsayılan 2" },
            "saat": { "type": "integer", "description": "Anlaşma yoksa harcanan saat sayısı" },
            "arabulucu_sayisi": { "type": "integer", "description": "Varsayılan 1; birden fazla arabulucuda anlaşma dilim yüzdeleri artar" },
            "dava_sarti": { "type": "boolean", "description": "Dava şartı arabuluculuk mu (varsayılan evet). Hayır ise (ihtiyari arabuluculuk) anlaşma halinde asgari ücret tabanı uygulanmaz." }
          },
          "required": ["uyusmazlik_turu", "anlasma"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_arabuluculuk_ucreti",
        "2026 Arabuluculuk Asgari Ücret Tarifesi'ne (RG 26.12.2025/33119) göre ücret hesaplar: anlaşma yoksa taraf sayısına göre saatlik ücret, anlaşma varsa kademeli nispi ücret (asgari anlaşma ücreti tabanıyla).",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new MediationFeeRequest(
            ToolArgs.String(arguments, "uyusmazlik_turu") ?? "",
            ToolArgs.Bool(arguments, "anlasma"),
            ToolArgs.Decimal(arguments, "anlasma_tutari"),
            ToolArgs.Int(arguments, "taraf_sayisi"),
            ToolArgs.Int(arguments, "saat"),
            ToolArgs.Int(arguments, "arabulucu_sayisi"),
            arguments.TryGetProperty("dava_sarti", out var davaSarti) && davaSarti.ValueKind == JsonValueKind.False ? false : (bool?)null);

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcMediationFee(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

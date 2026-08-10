using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Yasal faiz, avans (ticari temerrüt) faizi veya özel oranla basit faiz hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class InterestTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "faiz_turu": {
              "type": "string",
              "enum": ["yasal", "avans", "ozel"],
              "description": "yasal = 3095 m.1 kanuni faiz; avans = ticari işlerde temerrüt (TCMB avans oranı); ozel = ozel_oran ile sabit oran"
            },
            "ana_para": { "type": "number", "description": "TL" },
            "baslangic": { "type": "string", "format": "date", "description": "Faiz başlangıcı (temerrüt tarihi)" },
            "bitis": { "type": "string", "format": "date", "description": "Hesap sonu tarihi" },
            "bitis_dahil": { "type": "boolean", "description": "Bitiş günü de faize dahil edilsin mi (varsayılan hayır)" },
            "ozel_oran": { "type": "number", "description": "faiz_turu=ozel iken yıllık %" }
          },
          "required": ["faiz_turu", "ana_para", "baslangic", "bitis"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_faiz",
        "Yasal faiz, avans (ticari temerrüt) faizi veya özel oranla basit faiz hesaplar (ACT/365, dönem tablosuna göre parçalı). Oran dönemleri günceldir; 31.07.2026 sonrası yasal faiz %31'dir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new InterestRequest(
            ToolArgs.String(arguments, "faiz_turu") ?? "",
            ToolArgs.Decimal(arguments, "ana_para") ?? -1m,
            ToolArgs.Date(arguments, "baslangic"),
            ToolArgs.Date(arguments, "bitis"),
            ToolArgs.Bool(arguments, "bitis_dahil"),
            ToolArgs.Decimal(arguments, "ozel_oran"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcInterest(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

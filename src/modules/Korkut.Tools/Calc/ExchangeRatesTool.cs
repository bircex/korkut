using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>TCMB günlük gösterge döviz kurlarını (döviz alış/satış) getirir. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class ExchangeRatesTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "para_birimleri": {
              "type": "array",
              "items": { "type": "string" },
              "description": "ISO 4217 kodları (örn. USD, EUR, GBP, CHF, JPY); verilmezse USD, EUR, GBP"
            },
            "tarih": {
              "type": "string",
              "format": "date",
              "description": "Kur tarihi; verilmezse en güncel bülten. Hafta sonu ve tatillerde bir önceki iş günü bülteni kullanılır"
            }
          }
        }
        """);

    public ToolSpec Spec => new(
        "get_exchange_rates",
        "TCMB günlük gösterge döviz kurlarını getirir (döviz alış/satış, TL). Tarih verilirse o güne ait bülten; "
        + "hafta sonu ve resmi tatillerde bir önceki iş günü bülteni kullanılır ve gerçek bülten tarihi kur_tarihi alanında döner.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        List<string>? codes = null;
        if (arguments.TryGetProperty("para_birimleri", out var requested) && requested.ValueKind == JsonValueKind.Array)
        {
            codes = requested.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!)
                .ToList();
        }

        var request = new ExchangeRatesRequest(codes, ToolArgs.Date(arguments, "tarih"));
        try
        {
            return ToolResult.Ok(ToolArgs.Result(await calc.GetExchangeRatesAsync(request, ct)));
        }
        catch (BadRequestException ex)
        {
            return ToolResult.Error(ex.Message);
        }
    }
}

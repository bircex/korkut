using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>2026 trafik idari para cezalarını (2918 sK, 7574 sK ile değişik) ihlal türüne göre hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class TrafficFineTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "ihlal_turu": {
              "type": "string",
              "enum": ["hiz", "emniyet_kemeri", "alkol", "kirmizi_isik", "cep_telefonu", "trafik_sigortasi_yok", "muayenesiz"],
              "description": "Trafik ihlalinin türü"
            },
            "hiz_asim_km_sa": { "type": "integer", "description": "hiz ihlali için zorunlu: tespit edilen hız aşımı, km/s" },
            "yerlesim_yeri_ici": { "type": "boolean", "description": "hiz ihlali için: yerleşim yeri içinde mi (varsayılan evet)" },
            "tekrar": { "type": "integer", "description": "alkol/kirmizi_isik/cep_telefonu/muayenesiz için: kaçıncı tespit (varsayılan 1)" }
          },
          "required": ["ihlal_turu"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_trafik_cezasi",
        "2026 trafik idari para cezalarını (2918 sK, 7574 sK ile değişik, RG 27.02.2026/33181) hesaplar: hız aşımı, emniyet kemeri, alkol, kırmızı ışık, cep telefonu, sigortasız/muayenesiz araç; peşin ödeme (%25 indirimli) tutarını da döner.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new TrafficFineRequest(
            ToolArgs.String(arguments, "ihlal_turu") ?? "",
            ToolArgs.Int(arguments, "hiz_asim_km_sa"),
            arguments.TryGetProperty("yerlesim_yeri_ici", out var settlement) && settlement.ValueKind == JsonValueKind.False ? false : (bool?)null,
            ToolArgs.Int(arguments, "tekrar"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcTrafficFine(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

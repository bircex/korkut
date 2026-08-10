using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Nafaka tahmini: yerleşik uygulama bantlarına göre aralık verir; bağlayıcı formül yoktur. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class AlimonyTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "nafaka_turu": {
              "type": "string",
              "enum": ["yoksulluk", "istirak", "tedbir"],
              "description": "yoksulluk = boşanma sonrası eş lehine (TMK 175); istirak = çocuk için (TMK 182/330); tedbir = dava süresince (TMK 169)"
            },
            "yukumlu_net_gelir": { "type": "number", "description": "Nafaka yükümlüsünün aylık net geliri, TL" },
            "cocuk_sayisi": { "type": "integer", "description": "İştirak nafakası hesaplanacak çocuk sayısı (istirak için zorunlu)" },
            "es_calisiyor": { "type": "boolean", "description": "Nafaka talep eden eş çalışıyor mu (yoksulluk koşulu değerlendirmesi için)" }
          },
          "required": ["nafaka_turu", "yukumlu_net_gelir"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_nafaka",
        "Nafaka için TAHMİNİ bir aralık verir (yoksulluk, iştirak veya tedbir). Yerleşik uygulamada yoksulluk nafakası gelirin %20-25'i, iştirak nafakası çocuk başına %15-20'si bandında seyreder ve toplam nafakalar gelirin yaklaşık yarısını aşmaz; kesin tutar hakimin takdirindedir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new AlimonyRequest(
            ToolArgs.String(arguments, "nafaka_turu") ?? "",
            ToolArgs.Decimal(arguments, "yukumlu_net_gelir") ?? -1m,
            ToolArgs.Int(arguments, "cocuk_sayisi"),
            ToolArgs.Bool(arguments, "es_calisiyor"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcAlimony(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

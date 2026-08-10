using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Zamanaşımı hesabı: TBK alacak zamanaşımları ile TCK dava ve ceza (infaz) zamanaşımı. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class StatuteOfLimitationsTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "alan": {
              "type": "string",
              "enum": ["tbk_genel", "haksiz_fiil", "kira", "ticari_satis", "ceza_dava", "ceza_infaz"],
              "description": "Zamanaşımı alanı: TBK genel alacak, haksız fiil, kira/dönemsel edim, ticari satış, ceza davası (TCK m.66) veya ceza infazı (TCK m.68)"
            },
            "olay_tarihi": { "type": "string", "format": "date", "description": "Sürenin başladığı tarih: alacağın muacceliyeti, haksız fiil/suç tarihi veya hükmün kesinleşmesi" },
            "ceza_ust_siniri_yil": { "type": "integer", "description": "ceza_dava için suçun kanuni üst sınırı, ceza_infaz için hükmedilen ceza (yıl); süreli hapiste zorunlu" },
            "ceza_turu": {
              "type": "string",
              "enum": ["sureli", "muebbet", "agirlastirilmis_muebbet"],
              "description": "Ceza alanlarında ceza türü (varsayılan sureli; müebbetlerde ceza_ust_siniri_yil gerekmez)"
            }
          },
          "required": ["alan", "olay_tarihi"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_zamanasimi",
        "Zamanaşımı süresini ve tahmini son tarihi hesaplar: TBK genel (10 yıl), haksız fiil (2/10 yıl), kira ve dönemsel edimler (5 yıl), ticari satış, TCK dava zamanaşımı (m.66) ve ceza infaz zamanaşımı (m.68). Kesme ve durma sebepleri hesaba katılmaz, çıktıdaki uyarıya bakın.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new StatuteOfLimitationsRequest(
            ToolArgs.String(arguments, "alan") ?? "",
            ToolArgs.Date(arguments, "olay_tarihi"),
            ToolArgs.Int(arguments, "ceza_ust_siniri_yil"),
            ToolArgs.String(arguments, "ceza_turu"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcStatuteOfLimitations(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Ceza infaz hesabı: koşullu salıverilme, denetimli serbestlik ve bihakkın tahliye tarihleri. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class SentenceExecutionTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "suc_tarihi": { "type": "string", "format": "date", "description": "Suçun işlendiği tarih (geçici m.10 ve çocuk kontrolü için)" },
            "dogum_tarihi": { "type": "string", "format": "date", "description": "Hükümlünün doğum tarihi (suç tarihinde 18 yaş altı kontrolü için, isteğe bağlı)" },
            "ceza_turu": {
              "type": "string",
              "enum": ["sureli", "muebbet", "agirlastirilmis_muebbet"],
              "description": "Hükmedilen cezanın türü"
            },
            "ceza_yil": { "type": "integer", "description": "Süreli hapiste ceza: yıl" },
            "ceza_ay": { "type": "integer", "description": "Süreli hapiste ceza: ay" },
            "ceza_gun": { "type": "integer", "description": "Süreli hapiste ceza: gün" },
            "suc_tipi": {
              "type": "string",
              "enum": ["adi", "kasten_oldurme", "cinsel", "uyusturucu_ticareti", "teror_orgutlu"],
              "description": "Koşullu salıverilme oranını belirleyen suç grubu (adi = genel rejim 1/2)"
            },
            "tekerrur": { "type": "integer", "enum": [0, 1, 2], "description": "Tekerrür durumu: 0 yok, 1 birinci mükerrir, 2 ikinci mükerrir" },
            "infaz_baslangic": { "type": "string", "format": "date", "description": "İnfazın başladığı (cezaevine giriş) tarih" },
            "mahsup_gun": { "type": "integer", "description": "Tutuklulukta geçen ve mahsup edilecek gün sayısı (TCK m.63)" },
            "ozel_durum": {
              "type": "string",
              "enum": ["yok", "cocuklu_kadin", "agir_hasta_yasli"],
              "description": "Denetimli serbestlik süresini uzatan özel durum: 0-6 yaş çocuklu kadın (2 yıl) veya ağır hasta/yaşlı (3 yıl)"
            }
          },
          "required": ["suc_tarihi", "ceza_turu", "suc_tipi", "infaz_baslangic"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_infaz",
        "Ceza infaz hesabı yapar: koşullu salıverilme tarihi, denetimli serbestlik (105/A ve 7571 sK ile değişik geçici m.10 dahil) tarihi, bihakkın tahliye ve cezaevinde geçecek net süre. Mahsup günlerini düşer, tekerrür ve suç tipine göre oran uygular.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new SentenceExecutionRequest(
            ToolArgs.Date(arguments, "suc_tarihi"),
            ToolArgs.Date(arguments, "dogum_tarihi"),
            ToolArgs.String(arguments, "ceza_turu") ?? "",
            ToolArgs.Int(arguments, "ceza_yil"),
            ToolArgs.Int(arguments, "ceza_ay"),
            ToolArgs.Int(arguments, "ceza_gun"),
            ToolArgs.String(arguments, "suc_tipi") ?? "",
            ToolArgs.Int(arguments, "tekerrur"),
            ToolArgs.Date(arguments, "infaz_baslangic"),
            ToolArgs.Int(arguments, "mahsup_gun"),
            ToolArgs.String(arguments, "ozel_durum"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcSentenceExecution(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

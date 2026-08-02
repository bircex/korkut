using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Zamanaşımı hesabı: TBK alacak zamanaşımları ile TCK dava ve ceza (infaz) zamanaşımı.</summary>
public sealed class ZamanasimiTool : IAgentTool
{
    private const string Kaynak =
        "TBK m.146 (genel 10 yıl), m.147 (5 yıllık süreler), m.72 (haksız fiil 2/10 yıl); TCK m.66 (dava zamanaşımı), m.67 (durma-kesilme), m.68 (ceza zamanaşımı)";

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
        var alan = ToolArgs.String(arguments, "alan");
        if (alan is null || ToolArgs.Date(arguments, "olay_tarihi") is not { } olay)
        {
            return Task.FromResult(ToolResult.Error("alan ve olay_tarihi zorunludur."));
        }

        var uyarilar = new List<string>();
        int sureYil;
        string dayanak;
        int? uzunSureYil = null;

        switch (alan)
        {
            case "tbk_genel":
                sureYil = 10;
                dayanak = "TBK m.146: kanunda aksine hüküm yoksa her alacak 10 yıllık zamanaşımına tabidir.";
                break;
            case "haksiz_fiil":
                sureYil = 2;
                uzunSureYil = 10;
                dayanak = "TBK m.72: zarar görenin zararı ve tazminat yükümlüsünü öğrenmesinden itibaren 2 yıl, her halde fiilden itibaren 10 yıl.";
                uyarilar.Add("2 yıllık süre öğrenme tarihinden işler; burada öğrenme tarihi olay tarihi varsayıldı. Fiil aynı zamanda suç oluşturuyorsa daha uzun olan ceza dava zamanaşımı uygulanır (TBK m.72/1).");
                break;
            case "kira":
                sureYil = 5;
                dayanak = "TBK m.147/1: kira bedelleri, anapara faizleri ve ücret gibi dönemsel edimler 5 yıllık zamanaşımına tabidir.";
                break;
            case "ticari_satis":
                sureYil = 5;
                dayanak = "TBK m.147/3: perakende satışlardan doğan alacaklar 5 yıllık zamanaşımına tabidir.";
                uyarilar.Add("Tacirler arası/toptan satış sözleşmesinden doğan alacaklarda genel 10 yıllık süre (TBK m.146), ayıptan sorumlulukta ise 2 yıllık süre (TBK m.231) uygulanabilir; sözleşmenin niteliği belirleyicidir.");
                break;
            case "ceza_dava":
            case "ceza_infaz":
            {
                var cezaTuru = ToolArgs.String(arguments, "ceza_turu") ?? "sureli";
                var ustSinir = ToolArgs.Int(arguments, "ceza_ust_siniri_yil");
                if (cezaTuru == "sureli" && ustSinir is not > 0)
                {
                    return Task.FromResult(ToolResult.Error("Süreli hapiste ceza_ust_siniri_yil (pozitif yıl) zorunludur."));
                }

                if (alan == "ceza_dava")
                {
                    (sureYil, dayanak) = cezaTuru switch
                    {
                        "agirlastirilmis_muebbet" => (30, "TCK m.66/1-a: ağırlaştırılmış müebbet gerektiren suçlarda dava zamanaşımı 30 yıl."),
                        "muebbet" => (25, "TCK m.66/1-b: müebbet hapis gerektiren suçlarda dava zamanaşımı 25 yıl."),
                        _ => ustSinir >= 20
                            ? (20, "TCK m.66/1-c: 20 yıldan aşağı olmayan hapis gerektiren suçlarda dava zamanaşımı 20 yıl.")
                            : ustSinir > 5
                                ? (15, "TCK m.66/1-d: 5 yıldan fazla, 20 yıldan az hapis gerektiren suçlarda dava zamanaşımı 15 yıl.")
                                : (8, "TCK m.66/1-e: 5 yıldan fazla olmayan hapis veya adli para cezası gerektiren suçlarda dava zamanaşımı 8 yıl."),
                    };
                    uyarilar.Add("Süre, suçun kanundaki cezasının üst sınırına göre belirlenir (TCK m.66/3, ağırlaştırıcı nitelikli haller dahil). Fiili işlediğinde 12-15 yaş grubundakiler için sürelerin yarısı, 15-18 yaş için üçte ikisi uygulanır (m.66/2). Kesilme halinde süre en fazla yarısına kadar uzar (m.67/4).");
                }
                else
                {
                    (sureYil, dayanak) = cezaTuru switch
                    {
                        "agirlastirilmis_muebbet" => (40, "TCK m.68/1-a: ağırlaştırılmış müebbet hapiste ceza zamanaşımı 40 yıl."),
                        "muebbet" => (30, "TCK m.68/1-b: müebbet hapiste ceza zamanaşımı 30 yıl."),
                        _ => ustSinir >= 20
                            ? (24, "TCK m.68/1-c: 20 yıl ve daha fazla süreli hapiste ceza zamanaşımı 24 yıl.")
                            : ustSinir > 5
                                ? (20, "TCK m.68/1-d: 5 yıldan fazla hapiste ceza zamanaşımı 20 yıl.")
                                : (10, "TCK m.68/1-e: 5 yıla kadar hapis ve adli para cezalarında ceza zamanaşımı 10 yıl."),
                    };
                    uyarilar.Add("Süre hükmün kesinleştiği veya infazın kesintiye uğradığı günden işler (TCK m.68/5); olay_tarihi olarak kesinleşme tarihi girilmelidir. 12-18 yaş indirimleri m.68/2'de saklıdır.");
                }

                break;
            }

            default:
                return Task.FromResult(ToolResult.Error("Geçersiz alan."));
        }

        uyarilar.Add(
            "Zamanaşımını kesen ve durduran sebepler (TBK m.152-154: dava, icra takibi, borç ikrarı; TCK m.67: iddianame, sorgu, mahkumiyet vb.) hesaba katılmadı; kesin değerlendirme dosya üzerinden yapılmalıdır. Bu bir tahmindir, hukuki dayanak değildir.");

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Alan = alan,
            SureYil = sureYil,
            TahminiSonTarih = olay.AddYears(sureYil),
            UzunSureYil = uzunSureYil,
            UzunSureSonTarih = uzunSureYil is { } u ? olay.AddYears(u) : (DateOnly?)null,
            Dayanak = dayanak,
            Uyarilar = uyarilar,
            Kaynak = Kaynak,
        })));
    }
}

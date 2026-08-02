using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Nafaka tahmini: yerleşik uygulama bantlarına göre aralık verir; bağlayıcı formül yoktur.</summary>
public sealed class NafakaTool : IAgentTool
{
    private const string Kaynak =
        "TMK m.169 (tedbir nafakası), m.175 (yoksulluk nafakası), m.182 ve m.330 (iştirak nafakası); oran bantları yerleşik uygulamadan alınmış tahminlerdir, kanuni bir formül yoktur";

    /// <summary>Yerleşik uygulama bantları (gelirin yüzdesi).</summary>
    private const decimal YoksullukAlt = 0.20m;

    private const decimal YoksullukUst = 0.25m;

    private const decimal IstirakCocukBasiAlt = 0.15m;

    private const decimal IstirakCocukBasiUst = 0.20m;

    /// <summary>Toplam nafakaların gelire oranı için uygulamada gözetilen tavan.</summary>
    private const decimal ToplamTavan = 0.50m;

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
        var tur = ToolArgs.String(arguments, "nafaka_turu");
        if (tur is not ("yoksulluk" or "istirak" or "tedbir") || ToolArgs.Decimal(arguments, "yukumlu_net_gelir") is not { } gelir || gelir <= 0)
        {
            return Task.FromResult(ToolResult.Error("Geçerli nafaka_turu ve pozitif yukumlu_net_gelir zorunludur."));
        }

        var uyarilar = new List<string>();
        decimal altOran;
        decimal ustOran;
        string bandAciklama;

        if (tur == "istirak")
        {
            if (ToolArgs.Int(arguments, "cocuk_sayisi") is not { } cocuk || cocuk <= 0)
            {
                return Task.FromResult(ToolResult.Error("İştirak nafakası için pozitif cocuk_sayisi zorunludur."));
            }

            altOran = IstirakCocukBasiAlt * cocuk;
            ustOran = IstirakCocukBasiUst * cocuk;
            bandAciklama = $"Çocuk başına %{IstirakCocukBasiAlt * 100:0}-{IstirakCocukBasiUst * 100:0} bandı x {cocuk} çocuk";
            uyarilar.Add("İştirak nafakası çocuğun yaşı, eğitim gideri ve tarafların mali gücüne göre belirlenir; çocuk büyüdükçe artırım davası açılabilir (TMK m.331).");
        }
        else
        {
            altOran = YoksullukAlt;
            ustOran = YoksullukUst;
            bandAciklama = $"Gelirin %{YoksullukAlt * 100:0}-{YoksullukUst * 100:0} bandı";
            if (tur == "yoksulluk")
            {
                uyarilar.Add("Yoksulluk nafakası için talep edenin boşanma yüzünden yoksulluğa düşecek olması ve kusurunun daha ağır olmaması gerekir (TMK m.175).");
                if (ToolArgs.Bool(arguments, "es_calisiyor"))
                {
                    uyarilar.Add("Talep eden eş çalışıyor: düzenli ve yeterli geliri varsa yoksulluk koşulu oluşmayabilir ve nafaka hiç takdir edilmeyebilir.");
                }
            }
            else
            {
                uyarilar.Add("Tedbir nafakası dava süresince geçerlidir ve kusur aranmaz (TMK m.169); müşterek çocuklar için ayrıca çocuk başına %15-20 bandında tedbir nafakası eklenebilir.");
            }
        }

        var tavanUygulandi = false;
        if (ustOran > ToplamTavan)
        {
            ustOran = ToplamTavan;
            tavanUygulandi = true;
        }

        if (altOran > ToplamTavan)
        {
            altOran = ToplamTavan;
            tavanUygulandi = true;
        }

        if (tavanUygulandi)
        {
            uyarilar.Add($"Toplam nafakaların gelirin yaklaşık %{ToplamTavan * 100:0}'sini aşmaması yönündeki uygulama gözetilerek aralık tavandan kırpıldı.");
        }

        uyarilar.Add(
            "BU BİR TAHMİNDİR, HUKUKİ DAYANAK DEĞİLDİR: nafakada yerleşik bir formül yoktur; tutar, tarafların sosyoekonomik durumu, ihtiyaçlar ve hakkaniyet gözetilerek tamamen hakimin takdiriyle belirlenir. Emsal kararlar ve tarafların yaşam düzeyi somut dosyada incelenmelidir.");

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            NafakaTuru = tur,
            YukumluNetGelir = gelir,
            OranBandi = bandAciklama,
            TahminiAltSinirTl = decimal.Round(gelir * altOran, 2),
            TahminiUstSinirTl = decimal.Round(gelir * ustOran, 2),
            Uyarilar = uyarilar,
            Kaynak = Kaynak,
        })));
    }
}

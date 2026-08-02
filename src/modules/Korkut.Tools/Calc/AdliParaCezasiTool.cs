using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Adli para cezası hesabı: gün sayısı x günlük miktar, hapse çevirme karşılığı ve taksit kuralı.</summary>
public sealed class AdliParaCezasiTool : IAgentTool
{
    /// <summary>TCK m.52/2, 7499 sK (RG 12.03.2024) ile değişik: günlük miktar alt/üst sınırı.</summary>
    private const decimal GunlukAltSinir = 100m;

    private const decimal GunlukUstSinir = 500m;

    /// <summary>TCK m.52/1: tam gün sayısı alt/üst sınırı.</summary>
    private const int GunAltSinir = 5;

    private const int GunUstSinir = 730;

    private const string Kaynak =
        "TCK m.52 (günlük miktar 100-500 TL: 7499 sK m.9, RG 12.03.2024/32487; 12.03.2024 öncesi işlenen suçlarda 20-100 TL); 5275 sK m.106 (ödenmeyen adli para cezasının hapse çevrilmesi)";

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "gun_sayisi": { "type": "integer", "description": "Hükmedilen tam gün sayısı (TCK m.52/1: 5-730 gün)" },
            "gunluk_miktar": { "type": "number", "description": "Bir gün karşılığı miktar, TL (TCK m.52/2: güncel aralık 100-500 TL)" },
            "taksit": { "type": "boolean", "description": "Taksitle ödeme bilgisi de istenirse true (TCK m.52/4)" }
          },
          "required": ["gun_sayisi", "gunluk_miktar"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_adli_para_cezasi",
        "Adli para cezasını hesaplar: gün sayısı x günlük miktar = toplam ceza, ödenmemesi halinde hapse çevirme karşılığı (gün) ve istenirse taksit kuralı (TCK m.52, 5275 m.106).",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var gunSayisi = ToolArgs.Int(arguments, "gun_sayisi");
        var gunlukMiktar = ToolArgs.Decimal(arguments, "gunluk_miktar");
        if (gunSayisi is not { } gun || gun <= 0 || gunlukMiktar is not { } miktar || miktar <= 0)
        {
            return Task.FromResult(ToolResult.Error("Pozitif gun_sayisi ve gunluk_miktar zorunludur."));
        }

        var uyarilar = new List<string>();
        if (gun is < GunAltSinir or > GunUstSinir)
        {
            uyarilar.Add($"Gün sayısı TCK m.52/1'deki {GunAltSinir}-{GunUstSinir} gün aralığının dışında; kanunda daha yüksek gün öngören özel hükümler saklıdır.");
        }

        if (miktar is < GunlukAltSinir or > GunlukUstSinir)
        {
            uyarilar.Add($"Günlük miktar güncel {GunlukAltSinir:0}-{GunlukUstSinir:0} TL aralığının dışında (7499 sK, 12.03.2024 sonrası suçlar); 12.03.2024 öncesi işlenen suçlarda 20-100 TL aralığı uygulanır.");
        }

        var toplam = decimal.Round(gun * miktar, 2);
        string? taksitKurali = null;
        if (ToolArgs.Bool(arguments, "taksit"))
        {
            taksitKurali =
                $"TCK m.52/4: taksitle ödemeye karar verilebilir; taksit süresi 2 yılı geçemez ve taksit sayısı 4'ten az olamaz. Örnek 4 eşit taksit: {decimal.Round(toplam / 4, 2)} TL. Taksitlerden biri zamanında ödenmezse kalan kısmın tamamı tahsil edilir, ödenmeyen kısım hapse çevrilir.";
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            GunSayisi = gun,
            GunlukMiktar = miktar,
            ToplamCeza = toplam,
            HapseCevirmeGun = gun,
            HapseCevirmeAciklama =
                "Adli para cezası ödenmezse gün sayısı kadar hapse çevrilir (5275 m.106); kamuya yararlı işte çalışma seçeneği saklıdır.",
            TaksitKurali = taksitKurali,
            Uyarilar = uyarilar,
            Kaynak = Kaynak,
        })));
    }
}

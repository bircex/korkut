using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Yıllık izin süresi ve kullanılmayan izin ücretini hesaplar (4857 sK m.53, 57, 59).</summary>
public sealed class YillikIzinUcretiTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "kidem_yil": { "type": "number", "description": "Kıdem süresi, yıl olarak (örn. 6.5)" },
            "gunluk_brut_ucret": { "type": "number", "description": "Son günlük brüt çıplak ücret, TL (aylık brüt / 30)" },
            "kullanilmayan_izin_gunu": { "type": "number", "description": "Kullanılmayan izin günü toplamı (verilmezse kıdeme göre yıllık hak yazılır)" },
            "yas": { "type": "integer", "description": "İşçinin yaşı (opsiyonel; 18 ve altı ile 50 ve üzeri için asgari 20 gün uygulanır)" }
          },
          "required": ["kidem_yil", "gunluk_brut_ucret"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_yillik_izin_ucreti",
        "Yıllık ücretli izin hakkını (4857 sK m.53: 1-5 yıl dahil 14 gün, 5 yıldan fazla 15 yıldan az 20 gün, 15 yıl ve fazlası 26 gün) ve kullanılmayan izin ücretini hesaplar. Ücret, çıplak günlük brüt üzerinden hesaplanır; giydirilmiş ücret esas alınmaz.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var kidem = ToolArgs.Decimal(arguments, "kidem_yil");
        var gunlukBrut = ToolArgs.Decimal(arguments, "gunluk_brut_ucret");
        if (kidem is not { } kidemYil || gunlukBrut is not { } gunluk || gunluk <= 0)
        {
            return Task.FromResult(ToolResult.Error("kidem_yil ve pozitif gunluk_brut_ucret zorunludur."));
        }

        if (kidemYil < 1)
        {
            return Task.FromResult(ToolResult.Error(
                "Yıllık ücretli izin hakkı, en az 1 yıl çalışmayla doğar (4857 sK m.53). Bu kıdemle izin hakkı henüz doğmamıştır."));
        }

        // 4857 m.53: 1-5 yıl (beş yıl dahil) 14 gün; 5'ten fazla 15'ten az 20 gün; 15 (dahil) ve fazlası 26 gün.
        var hak = kidemYil <= 5 ? 14 : kidemYil < 15 ? 20 : 26;

        // 4857 m.53/son: 18 ve daha küçük yaştaki işçilerle 50 ve daha yukarı yaştaki işçilere
        // verilecek yıllık ücretli izin süresi 20 günden az olamaz.
        var yas = ToolArgs.Int(arguments, "yas");
        var yasKurali = yas is { } y && (y <= 18 || y >= 50);
        if (yasKurali && hak < 20)
        {
            hak = 20;
        }

        var gun = ToolArgs.Decimal(arguments, "kullanilmayan_izin_gunu") ?? hak;
        if (gun < 0)
        {
            return Task.FromResult(ToolResult.Error("kullanilmayan_izin_gunu negatif olamaz."));
        }

        var brutUcret = decimal.Round(gun * gunluk, 2);

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            KidemYil = kidemYil,
            YillikIzinHakkiGun = hak,
            YasKuraliUygulandi = yasKurali,
            HesaplananGun = gun,
            GunlukBrutUcret = gunluk,
            BrutIzinUcreti = brutUcret,
            Not = "Yıllık izin ücreti çıplak brüt ücret üzerinden hesaplanır (giydirilmiş ücret esas alınmaz, 4857 sK m.57). Sözleşme sona erdiğinde kullanılmayan izin ücreti gelir vergisi, damga vergisi ve SGK kesintilerine tabidir; tutar brüttür.",
            Kaynak = "4857 sK m.53 (izin süreleri), m.57 (izin ücreti) ve m.59 (sözleşme sonunda izin alacağı)",
        })));
    }
}

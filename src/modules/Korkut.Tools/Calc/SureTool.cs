using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Usul süresi hesabı: kanun yolu ve cevap sürelerinin son gününü tatil kurallarıyla birlikte bulur.</summary>
public sealed class SureTool : IAgentTool
{
    /// <summary>Bir sürenin tanımı: gün/hafta/yıl cinsinden uzunluk, dayanak ve tatil rejimi.</summary>
    private sealed record SureTanimi(int Gun, int Hafta, int Yil, string Dayanak, bool AdliTatildeUzar, bool IdariAraVerme);

    private const string Kaynak =
        "HMK m.92-93, m.104, m.127, m.345, m.361; CMK m.273, m.291; İİK m.62, m.67; İYUK m.8, m.45, m.46, m.61; " + CourtCalendar.Kaynak;

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "sure_turu": {
              "type": "string",
              "enum": ["istinaf_hukuk", "istinaf_ceza", "temyiz_hukuk", "temyiz_ceza", "cevap_dilekcesi", "icra_itiraz", "itirazin_iptali", "istinaf_idare", "temyiz_idare"],
              "description": "Hesaplanacak sürenin türü"
            },
            "teblig_tarihi": { "type": "string", "format": "date", "description": "Kararın/ödeme emrinin/dava dilekçesinin tebliğ tarihi" }
          },
          "required": ["sure_turu", "teblig_tarihi"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_sure",
        "Usul sürelerinin son gününü hesaplar: hukuk/ceza/idare istinaf ve temyiz, cevap dilekçesi, icra takibine itiraz ve itirazın iptali. Süre tebliğin ertesi günü başlar; son günün hafta sonu veya resmi tatile denk gelmesi ve adli tatil (HMK m.104) / çalışmaya ara verme (İYUK m.8/3) uzamaları uygulanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var sureTuru = ToolArgs.String(arguments, "sure_turu");
        if (sureTuru is null || ToolArgs.Date(arguments, "teblig_tarihi") is not { } teblig)
        {
            return Task.FromResult(ToolResult.Error("sure_turu ve teblig_tarihi zorunludur."));
        }

        SureTanimi? tanim = sureTuru switch
        {
            "istinaf_hukuk" => new(0, 2, 0, "HMK m.345: istinaf süresi 2 hafta", AdliTatildeUzar: true, IdariAraVerme: false),
            "istinaf_ceza" => new(7, 0, 0, "CMK m.273: istinaf süresi 7 gün", AdliTatildeUzar: false, IdariAraVerme: false),
            "temyiz_hukuk" => new(0, 2, 0, "HMK m.361: temyiz süresi 2 hafta", AdliTatildeUzar: true, IdariAraVerme: false),
            "temyiz_ceza" => new(15, 0, 0, "CMK m.291: temyiz süresi 15 gün", AdliTatildeUzar: false, IdariAraVerme: false),
            "cevap_dilekcesi" => new(0, 2, 0, "HMK m.127: cevap süresi 2 hafta", AdliTatildeUzar: true, IdariAraVerme: false),
            "icra_itiraz" => new(7, 0, 0, "İİK m.62: ödeme emrine itiraz 7 gün", AdliTatildeUzar: false, IdariAraVerme: false),
            "itirazin_iptali" => new(0, 0, 1, "İİK m.67: itirazın iptali davası 1 yıl", AdliTatildeUzar: false, IdariAraVerme: false),
            "istinaf_idare" => new(30, 0, 0, "İYUK m.45: istinaf süresi 30 gün", AdliTatildeUzar: false, IdariAraVerme: true),
            "temyiz_idare" => new(30, 0, 0, "İYUK m.46: temyiz süresi 30 gün", AdliTatildeUzar: false, IdariAraVerme: true),
            _ => null,
        };
        if (tanim is null)
        {
            return Task.FromResult(ToolResult.Error("Geçersiz sure_turu."));
        }

        var kurallar = new List<string>
        {
            "Süre tebliğ tarihinin ertesi günü işlemeye başlar (HMK m.92 / CMK m.39 / İİK m.19 / İYUK m.8).",
            tanim.Dayanak,
        };

        // Hafta/yıl olarak belirlenen süre son hafta/yıl içinde başladığı güne karşılık gelen günde,
        // gün olarak belirlenen süre son gün mesai bitiminde biter; hepsi tebliğe ekleme ile bulunur.
        var sonGun = teblig.AddYears(tanim.Yil).AddDays((tanim.Hafta * 7) + tanim.Gun);
        kurallar.Add($"Ham son gün: {sonGun:dd.MM.yyyy}.");

        if (tanim.AdliTatildeUzar && CourtCalendar.IsAdliTatil(sonGun))
        {
            sonGun = CourtCalendar.AdliTatilUzamasi(sonGun.Year);
            kurallar.Add($"Son gün adli tatile (20 Temmuz - 31 Ağustos) rastladı; süre tatilin bittiği günden itibaren 1 hafta uzadı (HMK m.104): {sonGun:dd.MM.yyyy}.");
        }

        if (tanim.IdariAraVerme && CourtCalendar.IsAdliTatil(sonGun))
        {
            sonGun = CourtCalendar.AdliTatilUzamasi(sonGun.Year);
            kurallar.Add($"Son gün çalışmaya ara verme dönemine (20 Temmuz - 31 Ağustos) rastladı; süre ara vermenin bittiği günü izleyen günden itibaren 7 gün uzadı (İYUK m.8/3, m.61): {sonGun:dd.MM.yyyy}.");
        }

        var uyarilar = new List<string>();
        while (true)
        {
            if (CourtCalendar.IsWeekend(sonGun))
            {
                kurallar.Add($"{sonGun:dd.MM.yyyy} hafta sonuna rastladığından süre ilk iş gününe uzadı (HMK m.93).");
                sonGun = sonGun.AddDays(1);
                continue;
            }

            if (CourtCalendar.HolidayName(sonGun) is { } tatil)
            {
                kurallar.Add($"{sonGun:dd.MM.yyyy} resmi tatile ({tatil}) rastladığından süre ilk iş gününe uzadı (HMK m.93).");
                sonGun = sonGun.AddDays(1);
                continue;
            }

            break;
        }

        if (!CourtCalendar.HasReligiousHolidayData(sonGun.Year))
        {
            uyarilar.Add($"{sonGun.Year} yılının dini bayram günleri tabloda yok; son gün bir dini bayrama denk geliyorsa süre ayrıca uzar, kontrol edin.");
        }

        uyarilar.Add(
            "Arife günleri ve 28 Ekim yarım gün tatildir ve burada ihtiyatlı yorumla süreyi uzatır kabul edildi; uygulamada yarım günde mesai öğlene kadar sürer. Ceza sürelerinde tefhimle başlama, idari yargıda ara kararlar ve özel kanunlardaki farklı süreler için dosya özelinde kontrol gerekir; süreler hak düşürücüdür.");

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            SureTuru = sureTuru,
            TebligTarihi = teblig,
            SonGun = sonGun,
            Kurallar = kurallar,
            Uyarilar = uyarilar,
            Kaynak = Kaynak,
        })));
    }
}

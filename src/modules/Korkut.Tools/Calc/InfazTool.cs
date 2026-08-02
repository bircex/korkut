using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Ceza infaz hesabı: koşullu salıverilme, denetimli serbestlik ve bihakkın tahliye tarihleri.</summary>
public sealed class InfazTool : IAgentTool
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

    private static readonly string[] SucTipleri = ["adi", "kasten_oldurme", "cinsel", "uyusturucu_ticareti", "teror_orgutlu"];

    private static readonly string[] OzelDurumlar = ["yok", "cocuklu_kadin", "agir_hasta_yasli"];

    public ToolSpec Spec => new(
        "calc_infaz",
        "Ceza infaz hesabı yapar: koşullu salıverilme tarihi, denetimli serbestlik (105/A ve 7571 sK ile değişik geçici m.10 dahil) tarihi, bihakkın tahliye ve cezaevinde geçecek net süre. Mahsup günlerini düşer, tekerrür ve suç tipine göre oran uygular.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var sucTarihi = ToolArgs.Date(arguments, "suc_tarihi");
        var cezaTuru = ToolArgs.String(arguments, "ceza_turu");
        var sucTipi = ToolArgs.String(arguments, "suc_tipi");
        var infazBaslangic = ToolArgs.Date(arguments, "infaz_baslangic");
        if (sucTarihi is not { } suc || cezaTuru is null || sucTipi is null || infazBaslangic is not { } baslangic)
        {
            return Task.FromResult(ToolResult.Error("suc_tarihi, ceza_turu, suc_tipi ve infaz_baslangic zorunludur."));
        }

        if (Array.IndexOf(SucTipleri, sucTipi) < 0)
        {
            return Task.FromResult(ToolResult.Error("suc_tipi adi, kasten_oldurme, cinsel, uyusturucu_ticareti veya teror_orgutlu olmalıdır."));
        }

        var tekerrur = ToolArgs.Int(arguments, "tekerrur") ?? 0;
        if (tekerrur is < 0 or > 2)
        {
            return Task.FromResult(ToolResult.Error("tekerrur 0, 1 veya 2 olmalıdır."));
        }

        var mahsup = ToolArgs.Int(arguments, "mahsup_gun") ?? 0;
        if (mahsup < 0)
        {
            return Task.FromResult(ToolResult.Error("mahsup_gun negatif olamaz."));
        }

        var ozelDurum = ToolArgs.String(arguments, "ozel_durum") ?? "yok";
        if (Array.IndexOf(OzelDurumlar, ozelDurum) < 0)
        {
            return Task.FromResult(ToolResult.Error("ozel_durum yok, cocuklu_kadin veya agir_hasta_yasli olmalıdır."));
        }

        var uyarilar = new List<string>();
        DateOnly kosulluSaliverilme;
        DateOnly? bihakkin;
        int? toplamCezaGun = null;
        string oranMetni;

        switch (cezaTuru)
        {
            case "sureli":
            {
                var yil = ToolArgs.Int(arguments, "ceza_yil") ?? 0;
                var ay = ToolArgs.Int(arguments, "ceza_ay") ?? 0;
                var gun = ToolArgs.Int(arguments, "ceza_gun") ?? 0;
                if (yil < 0 || ay < 0 || gun < 0 || yil + ay + gun == 0)
                {
                    return Task.FromResult(ToolResult.Error("Süreli cezada ceza_yil/ceza_ay/ceza_gun ile pozitif bir toplam ceza girilmelidir."));
                }

                var bihakkinHam = baslangic.AddYears(yil).AddMonths(ay).AddDays(gun);
                toplamCezaGun = bihakkinHam.DayNumber - baslangic.DayNumber;
                var (pay, payda) = InfazRates.KosulluSaliverilmeOrani(sucTipi, tekerrur);
                var kosulluGun = (int)Math.Ceiling(toplamCezaGun.Value * (decimal)pay / payda);
                kosulluSaliverilme = baslangic.AddDays(kosulluGun - mahsup);
                bihakkin = bihakkinHam.AddDays(-mahsup);
                oranMetni = $"{pay}/{payda}";
                break;
            }

            case "muebbet":
            case "agirlastirilmis_muebbet":
            {
                var kosulluYil = InfazRates.MuebbetKosulluYil(cezaTuru, tekerrur);
                kosulluSaliverilme = baslangic.AddYears(kosulluYil).AddDays(-mahsup);
                bihakkin = null;
                oranMetni = $"{kosulluYil} yıl";
                uyarilar.Add("Müebbet cezalarda bihakkın tahliye yoktur; infaz koşullu salıverilme ve denetim süresiyle sona erer.");
                break;
            }

            default:
                return Task.FromResult(ToolResult.Error("ceza_turu sureli, muebbet veya agirlastirilmis_muebbet olmalıdır."));
        }

        var denetimliYil = InfazRates.DenetimliSerbestlikYil(ozelDurum);
        if (InfazRates.GeciciM10DenetimliSerbestlikYil(suc, sucTipi) is { } geciciYil)
        {
            if (geciciYil > denetimliYil)
            {
                denetimliYil = geciciYil;
            }

            uyarilar.Add(
                "7571 sK ile değişik 5275 geçici m.10 uygulandı (suç tarihi 31.07.2023 ve öncesi): burada basitleştirilmiş olarak 30.03.2020 öncesi suçlarda 3 yıl, 30.03.2020 - 31.07.2023 arası suçlarda 2 yıl denetimli serbestlik esas alındı. Gerçek düzenleme ayrıca açık ceza infaz kurumuna 3 yıl erken ayrılma ve kademeli ek süreler içerir; kesin tarih infaz savcılığının müddetnamesine göre belirlenir. Geçici maddedeki istisna suçlar (kasten öldürme, cinsel dokunulmazlığa karşı suçlar vb.) için ayrıca kontrol gerekir.");
        }

        var denetimliSerbestlik = kosulluSaliverilme.AddYears(-denetimliYil);
        if (denetimliSerbestlik < baslangic)
        {
            denetimliSerbestlik = baslangic;
            uyarilar.Add("Hesaplanan denetimli serbestlik tarihi infaz başlangıcından önce; hükümlü koşulları sağlıyorsa doğrudan denetimli serbestlik değerlendirilebilir.");
        }

        if (ToolArgs.Date(arguments, "dogum_tarihi") is { } dogum && suc < dogum.AddYears(18))
        {
            uyarilar.Add("Suç tarihinde hükümlü 18 yaşından küçük: çocuklara özgü infaz rejimi (5275 m.107/5 artırımlı gün sayma, eğitimevi) uygulanmalıdır; bu hesap yetişkin rejimiyle yapılmıştır.");
        }

        uyarilar.Add(
            "Bu hesap iyi hal değerlendirmesi, disiplin cezaları, cezaların içtimaı, hücre cezası süreleri ve infaz hakimliği kararlarını dikkate almaz; bağlayıcı hesap infaz savcılığının düzenlediği müddetnamedir.");

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            CezaTuru = cezaTuru,
            SucTipi = sucTipi,
            ToplamCezaGun = toplamCezaGun,
            MahsupGun = mahsup,
            KosulluSaliverilmeOrani = oranMetni,
            KosulluSaliverilmeTarihi = kosulluSaliverilme,
            DenetimliSerbestlikSuresiYil = denetimliYil,
            DenetimliSerbestlikTarihi = denetimliSerbestlik,
            BihakkinTahliyeTarihi = bihakkin,
            CezaevindeNetGun = denetimliSerbestlik.DayNumber - baslangic.DayNumber,
            Uyarilar = uyarilar,
            Kaynak = InfazRates.Kaynak,
        })));
    }
}

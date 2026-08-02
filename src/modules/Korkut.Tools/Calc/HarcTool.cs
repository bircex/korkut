using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>2026 yargı harçlarını (başvurma + peşin karar-ilam + ıslah + kanun yolu + icra) kalem kalem hesaplar.</summary>
public sealed class HarcTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "islem": {
              "type": "string",
              "enum": ["dava_acilis", "islah_deger_artirimi", "istinaf", "temyiz", "icra_takip"],
              "description": "dava_acilis = ilk derece dava açılışı; islah_deger_artirimi = ıslah veya HMK 107 değer artırımı ek harcı; istinaf/temyiz = kanun yolu; icra_takip = ilamsız/ilamlı takip açılışı"
            },
            "dava_turu": {
              "type": "string",
              "enum": ["genel", "olum_cismani"],
              "description": "olum_cismani = ölüm ve cismani zarar tazminatı (açılışta peşin harç 1/20)"
            },
            "dava_degeri": { "type": "number", "description": "TL; nispi hesaplarda ve icra takibinde zorunlu" },
            "eski_deger": { "type": "number", "description": "TL; islah_deger_artirimi için" },
            "yeni_deger": { "type": "number", "description": "TL; islah_deger_artirimi için" },
            "maktu": { "type": "boolean", "description": "Konusu parayla ölçülemeyen dava (maktu karar-ilam harcı)" },
            "mahkeme": {
              "type": "string",
              "enum": ["sulh", "asliye_idare"],
              "description": "Başvurma harcı için mahkeme (varsayılan asliye_idare)"
            }
          },
          "required": ["islem"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_harc",
        "2026 yargı harçlarını (Harçlar Tebliği Seri No 98) kalem kalem hesaplar: dava açılış (başvurma + peşin nispi/maktu karar-ilam), ıslah/değer artırımı ek harcı, istinaf, temyiz ve icra takip harçları.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var islem = ToolArgs.String(arguments, "islem");
        var davaTuru = ToolArgs.String(arguments, "dava_turu") ?? "genel";
        if (davaTuru is not ("genel" or "olum_cismani"))
        {
            return Task.FromResult(ToolResult.Error("dava_turu genel veya olum_cismani olmalıdır."));
        }

        var pesinOran = davaTuru == "olum_cismani" ? CourtFees2026.OlumCismaniPesinOran : CourtFees2026.PesinOran;
        var deger = ToolArgs.Decimal(arguments, "dava_degeri");
        var kalemler = new List<HarcKalemi>();
        var notlar = new List<string>();

        switch (islem)
        {
            case "dava_acilis":
            {
                var basvurma = ToolArgs.String(arguments, "mahkeme") switch
                {
                    "sulh" => CourtFees2026.BasvurmaSulh,
                    _ => CourtFees2026.BasvurmaAsliyeIdare,
                };
                Ekle(kalemler, "Başvurma harcı", basvurma);
                if (ToolArgs.Bool(arguments, "maktu"))
                {
                    Ekle(kalemler, "Maktu karar ve ilam harcı (peşin)", CourtFees2026.MaktuKararIlam);
                }
                else
                {
                    if (deger is not { } d || d <= 0)
                    {
                        return Task.FromResult(ToolResult.Error("dava_acilis nispi hesabı için pozitif dava_degeri gerekir (maktu dava için maktu=true gönderin)."));
                    }

                    var kararIlam = d * CourtFees2026.KararIlamNispiOran;
                    var pesin = decimal.Round(kararIlam * pesinOran, 2);
                    Ekle(kalemler, davaTuru == "olum_cismani"
                        ? "Nispi karar ve ilam harcı peşini (1/20, ölüm/cismani zarar)"
                        : "Nispi karar ve ilam harcı peşini (1/4)", pesin);
                    notlar.Add($"Nispi karar ve ilam harcının tamamı (binde 68,31) {decimal.Round(kararIlam, 2)} TL'dir; kalan kısım karar kesinleşmeden önce tamamlatılır.");
                }

                break;
            }

            case "islah_deger_artirimi":
            {
                var eski = ToolArgs.Decimal(arguments, "eski_deger");
                var yeni = ToolArgs.Decimal(arguments, "yeni_deger");
                if (eski is not { } e || yeni is not { } y || e < 0 || y <= e)
                {
                    return Task.FromResult(ToolResult.Error("islah_deger_artirimi için eski_deger ve ondan büyük yeni_deger zorunludur."));
                }

                var ek = decimal.Round((y - e) * CourtFees2026.KararIlamNispiOran * pesinOran, 2);
                Ekle(kalemler, "Islah/değer artırımı ek peşin harcı", ek);
                notlar.Add($"Ek harç = (yeni değer - eski değer) x binde 68,31 x peşin oran; artırılan tutar {decimal.Round(y - e, 2)} TL.");
                break;
            }

            case "istinaf":
                Ekle(kalemler, "İstinaf kanun yoluna başvurma harcı", CourtFees2026.IstinafBasvurma);
                Ekle(kalemler, "İstinaf karar harcı (maktu)", CourtFees2026.MaktuKararIlam);
                break;

            case "temyiz":
                Ekle(kalemler, "Temyiz kanun yoluna başvurma harcı", CourtFees2026.TemyizBasvurma);
                Ekle(kalemler, "Temyiz karar harcı (maktu)", CourtFees2026.MaktuKararIlam);
                break;

            case "icra_takip":
            {
                if (deger is not { } d || d <= 0)
                {
                    return Task.FromResult(ToolResult.Error("icra_takip için pozitif dava_degeri (takip tutarı) gerekir."));
                }

                Ekle(kalemler, "İcra başvurma harcı", CourtFees2026.BasvurmaIcra);
                Ekle(kalemler, "İcra peşin harcı (binde 5)", decimal.Round(d * CourtFees2026.IcraPesinOran, 2));
                notlar.Add("Tahsil harcı tahsilat aşamasına göre ayrıca doğar: hacizden önce %4,55, hacizden sonra %9,10, satıştan sonra %11,38.");
                break;
            }

            default:
                return Task.FromResult(ToolResult.Error("islem dava_acilis, islah_deger_artirimi, istinaf, temyiz veya icra_takip olmalıdır."));
        }

        decimal toplam = 0;
        foreach (var k in kalemler)
        {
            toplam += k.Tutar;
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Islem = islem,
            DavaTuru = davaTuru,
            Kalemler = kalemler,
            Toplam = decimal.Round(toplam, 2),
            Notlar = notlar,
            TarifeDonemi = CourtFees2026.Donem,
            Kaynak = CourtFees2026.Kaynak,
        })));
    }

    /// <summary>Çıktıdaki tek harç kalemi (kalem adı + 2 haneye yuvarlanmış tutar).</summary>
    private sealed record HarcKalemi(string Kalem, decimal Tutar);

    private static void Ekle(List<HarcKalemi> kalemler, string ad, decimal tutar)
        => kalemler.Add(new HarcKalemi(ad, decimal.Round(tutar, 2)));
}

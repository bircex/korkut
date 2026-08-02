using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>2026 yargı harçlarını (başvurma + peşin karar-ilam + ıslah + kanun yolu + icra) kalem kalem hesaplar.</summary>
public sealed class CourtFeeTool : IAgentTool
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
        var operation = ToolArgs.String(arguments, "islem");
        var caseType = ToolArgs.String(arguments, "dava_turu") ?? "genel";
        if (caseType is not ("genel" or "olum_cismani"))
        {
            return Task.FromResult(ToolResult.Error("dava_turu genel veya olum_cismani olmalıdır."));
        }

        var advanceRate = caseType == "olum_cismani" ? CourtFees2026.OlumCismaniPesinOran : CourtFees2026.PesinOran;
        var value = ToolArgs.Decimal(arguments, "dava_degeri");
        var items = new List<CourtFeeItem>();
        var notes = new List<string>();

        switch (operation)
        {
            case "dava_acilis":
            {
                var filingFee = ToolArgs.String(arguments, "mahkeme") switch
                {
                    "sulh" => CourtFees2026.BasvurmaSulh,
                    _ => CourtFees2026.BasvurmaAsliyeIdare,
                };
                Add(items, "Başvurma harcı", filingFee);
                if (ToolArgs.Bool(arguments, "maktu"))
                {
                    Add(items, "Maktu karar ve ilam harcı (peşin)", CourtFees2026.MaktuKararIlam);
                }
                else
                {
                    if (value is not { } amount || amount <= 0)
                    {
                        return Task.FromResult(ToolResult.Error("dava_acilis nispi hesabı için pozitif dava_degeri gerekir (maktu dava için maktu=true gönderin)."));
                    }

                    var decisionFee = amount * CourtFees2026.KararIlamNispiOran;
                    var advance = decimal.Round(decisionFee * advanceRate, 2);
                    Add(items, caseType == "olum_cismani"
                        ? "Nispi karar ve ilam harcı peşini (1/20, ölüm/cismani zarar)"
                        : "Nispi karar ve ilam harcı peşini (1/4)", advance);
                    notes.Add($"Nispi karar ve ilam harcının tamamı (binde 68,31) {decimal.Round(decisionFee, 2)} TL'dir; kalan kısım karar kesinleşmeden önce tamamlatılır.");
                }

                break;
            }

            case "islah_deger_artirimi":
            {
                var oldValue = ToolArgs.Decimal(arguments, "eski_deger");
                var newValue = ToolArgs.Decimal(arguments, "yeni_deger");
                if (oldValue is not { } o || newValue is not { } n || o < 0 || n <= o)
                {
                    return Task.FromResult(ToolResult.Error("islah_deger_artirimi için eski_deger ve ondan büyük yeni_deger zorunludur."));
                }

                var additionalFee = decimal.Round((n - o) * CourtFees2026.KararIlamNispiOran * advanceRate, 2);
                Add(items, "Islah/değer artırımı ek peşin harcı", additionalFee);
                notes.Add($"Ek harç = (yeni değer - eski değer) x binde 68,31 x peşin oran; artırılan tutar {decimal.Round(n - o, 2)} TL.");
                break;
            }

            case "istinaf":
                Add(items, "İstinaf kanun yoluna başvurma harcı", CourtFees2026.IstinafBasvurma);
                Add(items, "İstinaf karar harcı (maktu)", CourtFees2026.MaktuKararIlam);
                break;

            case "temyiz":
                Add(items, "Temyiz kanun yoluna başvurma harcı", CourtFees2026.TemyizBasvurma);
                Add(items, "Temyiz karar harcı (maktu)", CourtFees2026.MaktuKararIlam);
                break;

            case "icra_takip":
            {
                if (value is not { } amount || amount <= 0)
                {
                    return Task.FromResult(ToolResult.Error("icra_takip için pozitif dava_degeri (takip tutarı) gerekir."));
                }

                Add(items, "İcra başvurma harcı", CourtFees2026.BasvurmaIcra);
                Add(items, "İcra peşin harcı (binde 5)", decimal.Round(amount * CourtFees2026.IcraPesinOran, 2));
                notes.Add("Tahsil harcı tahsilat aşamasına göre ayrıca doğar: hacizden önce %4,55, hacizden sonra %9,10, satıştan sonra %11,38.");
                break;
            }

            default:
                return Task.FromResult(ToolResult.Error("islem dava_acilis, islah_deger_artirimi, istinaf, temyiz veya icra_takip olmalıdır."));
        }

        decimal total = 0;
        foreach (var item in items)
        {
            total += item.Tutar;
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Islem = operation,
            DavaTuru = caseType,
            Kalemler = items,
            Toplam = decimal.Round(total, 2),
            Notlar = notes,
            TarifeDonemi = CourtFees2026.Donem,
            Kaynak = CourtFees2026.Kaynak,
        })));
    }

    /// <summary>Çıktıdaki tek harç kalemi (kalem adı + 2 haneye yuvarlanmış tutar).</summary>
    private sealed record CourtFeeItem(string Kalem, decimal Tutar);

    private static void Add(List<CourtFeeItem> items, string name, decimal amount)
        => items.Add(new CourtFeeItem(name, decimal.Round(amount, 2)));
}

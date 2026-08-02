using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// Kazalı aracın değer kaybını ZMSS Genel Şartları ekindeki resmi formülle hesaplar:
/// Toplam = (rayiç değer x %19) x hasar boyutu katsayısı x kilometre katsayısı.
/// </summary>
public sealed class VehicleValueLossTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "piyasa_degeri": { "type": "number", "description": "Aracın kaza tarihindeki hasarsız rayiç (piyasa) değeri, TL" },
            "kilometre": { "type": "integer", "description": "Aracın kaza tarihindeki kilometresi" },
            "hasar_boyutu": { "type": "string", "enum": ["buyuk", "orta", "kucuk", "basit"], "description": "Hasar boyutu (Genel Şartlar A1-A4). Verilmezse hasar_tutari'ndan türetilir" },
            "hasar_tutari": { "type": "number", "description": "Onarım (hasar) tutarı, TL; hasar_boyutu verilmezse boyut bundan türetilir (pratik yaklaşım)" }
          },
          "required": ["piyasa_degeri", "kilometre"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_arac_deger_kaybi",
        "Kazalı aracın değer kaybını ZMSS Genel Şartları ekindeki resmi formülle hesaplar: baz değer kaybı (rayiç x %19) x hasar boyutu katsayısı (büyük 0,90 / orta 0,75 / küçük 0,50 / basit 0,25) x kilometre katsayısı. Hasar boyutu verilmezse onarım tutarından türetilir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var marketValue = ToolArgs.Decimal(arguments, "piyasa_degeri");
        var mileage = ToolArgs.Int(arguments, "kilometre");
        if (marketValue is not { } value || value <= 0 || mileage is not { } mileageValue || mileageValue < 0)
        {
            return Task.FromResult(ToolResult.Error("Pozitif piyasa_degeri ve negatif olmayan kilometre zorunludur."));
        }

        var assumptions = new List<string>
        {
            "Resmi formül: Toplam Değer Kaybı = Baz Değer Kaybı (rayiç x %19) x hasar boyutu katsayısı x kilometre katsayısı (ZMSS Genel Şartları eki).",
        };

        var damageSize = ToolArgs.String(arguments, "hasar_boyutu");
        if (damageSize is null)
        {
            if (ToolArgs.Decimal(arguments, "hasar_tutari") is not { } repairAmount || repairAmount <= 0)
            {
                return Task.FromResult(ToolResult.Error("hasar_boyutu (buyuk|orta|kucuk|basit) veya pozitif hasar_tutari verilmelidir."));
            }

            var ratio = repairAmount / value;
            damageSize = ratio switch
            {
                <= 0.05m => "basit",
                <= 0.15m => "kucuk",
                <= 0.25m => "orta",
                _ => "buyuk",
            };
            assumptions.Add(
                "Hasar boyutu onarım tutarının rayice oranından türetildi (pratik yaklaşım, Genel Şartlar'da yer almaz): %5'e kadar basit, %15'e kadar küçük, %25'e kadar orta, üzeri büyük.");
        }

        if (Zmss.HasarBoyutuKatsayisi(damageSize) is not { } damageCoefficient)
        {
            return Task.FromResult(ToolResult.Error("hasar_boyutu buyuk, orta, kucuk veya basit olmalıdır."));
        }

        var mileageCoefficient = Zmss.KilometreKatsayisi(mileageValue) ?? 0m;
        var baseAmount = decimal.Round(value * Zmss.BazDegerKaybiOrani, 2);
        var valueLoss = decimal.Round(value * Zmss.BazDegerKaybiOrani * damageCoefficient * mileageCoefficient, 2);

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yontem = "ZMSS Genel Şartları eki resmi değer kaybı formülü",
            BazDegerKaybi = baseAmount,
            HasarBoyutu = damageSize,
            HasarBoyutuKatsayisi = damageCoefficient,
            KilometreKatsayisi = mileageCoefficient,
            DegerKaybi = valueLoss,
            Uyari = "Bu tutar sigorta tahkim/başvuru aşaması için Genel Şartlar formülüdür; mahkemede bilirkişi serbest piyasa (rayiç fark) yöntemiyle farklı ve çoğu kez daha yüksek bir tutara ulaşabilir.",
            Varsayimlar = assumptions,
            Kaynak = Zmss.DegerKaybiKaynak,
        })));
    }
}

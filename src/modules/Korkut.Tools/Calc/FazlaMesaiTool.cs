using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Fazla mesai, hafta tatili ve resmi tatil çalışma ücretlerini hesaplar (4857 sK m.41, 46, 47).</summary>
public sealed class FazlaMesaiTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "aylik_brut_ucret": { "type": "number", "description": "Aylık brüt ücret, TL" },
            "fazla_mesai_saat": { "type": "number", "description": "Normal fazla mesai saati (haftalık 45 saati aşan çalışma)" },
            "hafta_tatili_saat": { "type": "number", "description": "Hafta tatilinde çalışılan saat" },
            "resmi_tatil_gun": { "type": "number", "description": "Ulusal bayram ve genel tatilde çalışılan gün sayısı" }
          },
          "required": ["aylik_brut_ucret"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_fazla_mesai",
        "Fazla mesai (x1,5), hafta tatili çalışması (x2,5) ve ulusal bayram/genel tatil çalışması (günlük ücret x2) brüt alacaklarını hesaplar. Saatlik ücret aylık brüt / 225 kabulüyle bulunur; çıktı brüttür, vergi ve SGK kesintileri ayrıca uygulanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (ToolArgs.Decimal(arguments, "aylik_brut_ucret") is not { } aylik || aylik <= 0)
        {
            return Task.FromResult(ToolResult.Error("Pozitif aylik_brut_ucret zorunludur."));
        }

        var fmSaat = ToolArgs.Decimal(arguments, "fazla_mesai_saat") ?? 0m;
        var htSaat = ToolArgs.Decimal(arguments, "hafta_tatili_saat") ?? 0m;
        var rtGun = ToolArgs.Decimal(arguments, "resmi_tatil_gun") ?? 0m;
        if (fmSaat < 0 || htSaat < 0 || rtGun < 0)
        {
            return Task.FromResult(ToolResult.Error("Saat ve gün değerleri negatif olamaz."));
        }

        // Yerleşik uygulama: aylık ücret 225 saat karşılığıdır (haftalık 45 saat x 30/6),
        // saatlik ücret = aylık brüt / 225; günlük ücret = aylık brüt / 30.
        var saatlik = aylik / 225m;
        var gunluk = aylik / 30m;

        // 4857 m.41: fazla çalışma ücreti %50 zamlı (x1,5).
        var fazlaMesai = fmSaat * saatlik * 1.5m;

        // Hafta tatili çalışması: Yargıtay yerleşik içtihadı (9. HD), hafta tatilinde çalışan
        // işçinin çalışma karşılığı %50 zamlı 1,5 yevmiyeye ek olarak çalışmadan hak ettiği
        // 1 yevmiyeyle birlikte toplam 2,5 yevmiye aldığı yönündedir; saat bazında x2,5 uygulanır.
        // (x2 değil; x2 ulusal bayram/genel tatil rejimidir.)
        var haftaTatili = htSaat * saatlik * 2.5m;

        // 4857 m.47: UBGT gününde çalışan işçiye ayrıca 1 yevmiye ödenir; aylıkla ödenen
        // yevmiyeyle birlikte çalışılan her tatil günü toplam 2 yevmiyedir.
        var resmiTatil = rtGun * gunluk * 2m;

        var fm = decimal.Round(fazlaMesai, 2);
        var ht = decimal.Round(haftaTatili, 2);
        var rt = decimal.Round(resmiTatil, 2);

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            SaatlikUcret = decimal.Round(saatlik, 2),
            GunlukUcret = decimal.Round(gunluk, 2),
            FazlaMesaiBrut = fm,
            HaftaTatiliBrut = ht,
            ResmiTatilBrut = rt,
            ToplamBrut = fm + ht + rt,
            Not = "Tutarlar brüttür; ücret gibi gelir vergisi, damga vergisi ve SGK primine tabidir. Hafta tatili ve UBGT hesaplarında maktu aylık içinde ödenmiş 1 yevmiyelik kısım toplam hak edilen tutara dahildir; bakiye alacak hesabında ödenen kısım mahsup edilebilir.",
            Kaynak = "4857 sK m.41 (fazla çalışma %50 zamlı), m.46 (hafta tatili) ve m.47 (UBGT); hafta tatili x2,5 uygulaması Yargıtay 9. HD yerleşik içtihadı",
        })));
    }
}

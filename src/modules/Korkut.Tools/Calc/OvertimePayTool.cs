using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Fazla mesai, hafta tatili ve resmi tatil çalışma ücretlerini hesaplar (4857 sK m.41, 46, 47).</summary>
public sealed class OvertimePayTool : IAgentTool
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
        if (ToolArgs.Decimal(arguments, "aylik_brut_ucret") is not { } monthlyGross || monthlyGross <= 0)
        {
            return Task.FromResult(ToolResult.Error("Pozitif aylik_brut_ucret zorunludur."));
        }

        var overtimeHours = ToolArgs.Decimal(arguments, "fazla_mesai_saat") ?? 0m;
        var weeklyRestHours = ToolArgs.Decimal(arguments, "hafta_tatili_saat") ?? 0m;
        var holidayDays = ToolArgs.Decimal(arguments, "resmi_tatil_gun") ?? 0m;
        if (overtimeHours < 0 || weeklyRestHours < 0 || holidayDays < 0)
        {
            return Task.FromResult(ToolResult.Error("Saat ve gün değerleri negatif olamaz."));
        }

        // Yerleşik uygulama: aylık ücret 225 saat karşılığıdır (haftalık 45 saat x 30/6),
        // saatlik ücret = aylık brüt / 225; günlük ücret = aylık brüt / 30.
        var hourlyWage = monthlyGross / 225m;
        var dailyWage = monthlyGross / 30m;

        // 4857 m.41: fazla çalışma ücreti %50 zamlı (x1,5).
        var overtimePay = overtimeHours * hourlyWage * 1.5m;

        // Hafta tatili çalışması: Yargıtay yerleşik içtihadı (9. HD), hafta tatilinde çalışan
        // işçinin çalışma karşılığı %50 zamlı 1,5 yevmiyeye ek olarak çalışmadan hak ettiği
        // 1 yevmiyeyle birlikte toplam 2,5 yevmiye aldığı yönündedir; saat bazında x2,5 uygulanır.
        // (x2 değil; x2 ulusal bayram/genel tatil rejimidir.)
        var weeklyRestPay = weeklyRestHours * hourlyWage * 2.5m;

        // 4857 m.47: UBGT gününde çalışan işçiye ayrıca 1 yevmiye ödenir; aylıkla ödenen
        // yevmiyeyle birlikte çalışılan her tatil günü toplam 2 yevmiyedir.
        var holidayPay = holidayDays * dailyWage * 2m;

        var overtimePayRounded = decimal.Round(overtimePay, 2);
        var weeklyRestPayRounded = decimal.Round(weeklyRestPay, 2);
        var holidayPayRounded = decimal.Round(holidayPay, 2);

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            SaatlikUcret = decimal.Round(hourlyWage, 2),
            GunlukUcret = decimal.Round(dailyWage, 2),
            FazlaMesaiBrut = overtimePayRounded,
            HaftaTatiliBrut = weeklyRestPayRounded,
            ResmiTatilBrut = holidayPayRounded,
            ToplamBrut = overtimePayRounded + weeklyRestPayRounded + holidayPayRounded,
            Not = "Tutarlar brüttür; ücret gibi gelir vergisi, damga vergisi ve SGK primine tabidir. Hafta tatili ve UBGT hesaplarında maktu aylık içinde ödenmiş 1 yevmiyelik kısım toplam hak edilen tutara dahildir; bakiye alacak hesabında ödenen kısım mahsup edilebilir.",
            Kaynak = "4857 sK m.41 (fazla çalışma %50 zamlı), m.46 (hafta tatili) ve m.47 (UBGT); hafta tatili x2,5 uygulaması Yargıtay 9. HD yerleşik içtihadı",
        })));
    }
}

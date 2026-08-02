using System.Globalization;
using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// TBK m.344 kira artışı üst sınırını hesaplar: yenilenen kira döneminde artış, bir önceki kira
/// yılına ait TÜFE on iki aylık ortalamalara göre değişim oranını geçemez. Uygulamada yenileme
/// ayından bir önceki ayın (son açıklanan endeks ayının) oranı esas alınır.
/// </summary>
public sealed class KiraArtisTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "mevcut_kira": { "type": "number", "description": "Mevcut aylık kira bedeli, TL" },
            "yenileme_ayi": { "type": "string", "pattern": "^\\d{4}-\\d{2}$", "description": "Kira döneminin yenilendiği ay, YYYY-MM (örn. 2026-07)" },
            "tufe_orani": { "type": "number", "description": "İsteğe bağlı: uygulanacak 12 aylık ortalama TÜFE (%). Verilmezse gömülü TÜİK tablosundan bulunur" }
          },
          "required": ["mevcut_kira", "yenileme_ayi"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_kira_artis",
        "TBK m.344 kira artışı üst sınırını hesaplar: azami artış, yenileme ayından bir önceki ayın TÜİK 12 aylık "
        + "ortalama TÜFE değişim oranıdır. Konut ve çatılı işyeri kiralarının ikisinde de bu sınır uygulanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var kira = ToolArgs.Decimal(arguments, "mevcut_kira");
        var yenilemeAyi = ToolArgs.String(arguments, "yenileme_ayi");
        if (kira is not { } mevcutKira || mevcutKira <= 0 || yenilemeAyi is null)
        {
            return Task.FromResult(ToolResult.Error("Pozitif mevcut_kira ve yenileme_ayi (YYYY-MM) zorunludur."));
        }

        if (!DateOnly.TryParseExact(
                yenilemeAyi + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var yenileme))
        {
            return Task.FromResult(ToolResult.Error("yenileme_ayi YYYY-MM biçiminde olmalıdır (örn. 2026-07)."));
        }

        // TBK m.344 uygulaması: yenileme ayında son açıklanan endeks, bir önceki ayın endeksidir.
        var endeks = yenileme.AddMonths(-1);
        var ozelOran = ToolArgs.Decimal(arguments, "tufe_orani");
        if (ozelOran is { } verilen && verilen < 0)
        {
            return Task.FromResult(ToolResult.Error("tufe_orani negatif olamaz."));
        }

        var oran = ozelOran ?? TufeTwelveMonthAverage.For(endeks.Year, endeks.Month);
        if (oran is not { } uygulananOran)
        {
            return Task.FromResult(ToolResult.Error(
                $"{endeks.Year:0000}-{endeks.Month:00} endeks ayı için 12 aylık ortalama TÜFE gömülü tabloda yok; "
                + "TÜİK'in o ay için açıkladığı oranı tufe_orani parametresiyle verin."));
        }

        var artis = decimal.Round(mevcutKira * uygulananOran / 100m, 2);
        var kaynak = ozelOran is null
            ? $"{TufeTwelveMonthAverage.Kaynak} (endeks ayı {endeks.Year:0000}-{endeks.Month:00})"
            : "Kullanıcının verdiği TÜFE oranı; TBK m.344";
        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            MevcutKira = decimal.Round(mevcutKira, 2),
            UygulananOran = uygulananOran,
            EndeksAyi = $"{endeks.Year:0000}-{endeks.Month:00}",
            AzamiArtisTutari = artis,
            AzamiYeniKira = decimal.Round(mevcutKira + artis, 2),
            Not = "TBK m.344: artış üst sınırı bir önceki kira yılına ait 12 aylık ortalama TÜFE'dir; "
                + "çatılı işyeri kiralarında da TÜFE sınırı uygulanır. Taraflar daha düşük bir oran kararlaştırabilir.",
            Kaynak = kaynak,
        })));
    }
}

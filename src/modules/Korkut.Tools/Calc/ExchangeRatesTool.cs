using System.Text.Json;
using Korkut.Tools.Calc.Rates;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>TCMB günlük gösterge döviz kurlarını (döviz alış/satış) getirir.</summary>
public sealed class ExchangeRatesTool(TcmbRatesClient tcmb) : IAgentTool
{
    private static readonly string[] DefaultCurrencies = ["USD", "EUR", "GBP"];

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "para_birimleri": {
              "type": "array",
              "items": { "type": "string" },
              "description": "ISO 4217 kodları (örn. USD, EUR, GBP, CHF, JPY); verilmezse USD, EUR, GBP"
            },
            "tarih": {
              "type": "string",
              "format": "date",
              "description": "Kur tarihi; verilmezse en güncel bülten. Hafta sonu ve tatillerde bir önceki iş günü bülteni kullanılır"
            }
          }
        }
        """);

    public ToolSpec Spec => new(
        "get_exchange_rates",
        "TCMB günlük gösterge döviz kurlarını getirir (döviz alış/satış, TL). Tarih verilirse o güne ait bülten; "
        + "hafta sonu ve resmi tatillerde bir önceki iş günü bülteni kullanılır ve gerçek bülten tarihi kur_tarihi alanında döner.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public async Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var codes = DefaultCurrencies;
        if (arguments.TryGetProperty("para_birimleri", out var requested) && requested.ValueKind == JsonValueKind.Array)
        {
            codes = requested.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString()!.Trim().ToUpperInvariant())
                .Where(s => s.Length > 0)
                .Distinct()
                .ToArray();
            if (codes.Length == 0)
            {
                return ToolResult.Error("para_birimleri boş olamaz; en az bir ISO kodu verin (örn. USD).");
            }
        }

        var date = ToolArgs.Date(arguments, "tarih");
        TcmbBulletin? bulletin;
        try
        {
            bulletin = date is { } d ? await tcmb.GetForDateAsync(d, ct) : await tcmb.GetLatestAsync(ct);
        }
        catch (HttpRequestException)
        {
            return ToolResult.Error("TCMB kur servisine ulaşılamadı; lütfen daha sonra tekrar deneyin.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return ToolResult.Error("TCMB kur servisi zaman aşımına uğradı; lütfen daha sonra tekrar deneyin.");
        }

        if (bulletin is null)
        {
            return ToolResult.Error(
                "İstenen tarih için TCMB kur bülteni bulunamadı (7 güne kadar önceki iş günlerine de bakıldı); tarihi kontrol edin.");
        }

        var rateList = new List<object>(codes.Length);
        var missingCodes = new List<string>();
        foreach (var code in codes)
        {
            var rate = bulletin.Rates.FirstOrDefault(r =>
                string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));
            if (rate is null)
            {
                missingCodes.Add(code);
                continue;
            }

            rateList.Add(new
            {
                Kod = rate.Code,
                Birim = rate.Unit,
                Alis = rate.ForexBuying,
                Satis = rate.ForexSelling,
            });
        }

        if (rateList.Count == 0)
        {
            return ToolResult.Error(
                $"İstenen para birimleri TCMB bülteninde bulunamadı: {string.Join(", ", missingCodes)}.");
        }

        return ToolResult.Ok(ToolArgs.Result(new
        {
            KurTarihi = bulletin.Date,
            Kurlar = rateList,
            BulunamayanKodlar = missingCodes,
            Kaynak = "TCMB Gösterge Kurları",
        }));
    }
}

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

        var tarih = ToolArgs.Date(arguments, "tarih");
        TcmbBulletin? bulletin;
        try
        {
            bulletin = tarih is { } t ? await tcmb.GetForDateAsync(t, ct) : await tcmb.GetLatestAsync(ct);
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

        var kurlar = new List<object>(codes.Length);
        var bulunamayan = new List<string>();
        foreach (var code in codes)
        {
            var rate = bulletin.Rates.FirstOrDefault(r =>
                string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));
            if (rate is null)
            {
                bulunamayan.Add(code);
                continue;
            }

            kurlar.Add(new
            {
                Kod = rate.Code,
                Birim = rate.Unit,
                Alis = rate.ForexBuying,
                Satis = rate.ForexSelling,
            });
        }

        if (kurlar.Count == 0)
        {
            return ToolResult.Error(
                $"İstenen para birimleri TCMB bülteninde bulunamadı: {string.Join(", ", bulunamayan)}.");
        }

        return ToolResult.Ok(ToolArgs.Result(new
        {
            KurTarihi = bulletin.Date,
            Kurlar = kurlar,
            BulunamayanKodlar = bulunamayan,
            Kaynak = "TCMB Gösterge Kurları",
        }));
    }
}

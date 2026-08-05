using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Yıllık izin süresi ve kullanılmayan izin ücretini hesaplar (4857 sK m.53, 57, 59). Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class AnnualLeavePayTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "kidem_yil": { "type": "number", "description": "Kıdem süresi, yıl olarak (örn. 6.5)" },
            "gunluk_brut_ucret": { "type": "number", "description": "Son günlük brüt çıplak ücret, TL (aylık brüt / 30)" },
            "kullanilmayan_izin_gunu": { "type": "number", "description": "Kullanılmayan izin günü toplamı (verilmezse kıdeme göre yıllık hak yazılır)" },
            "yas": { "type": "integer", "description": "İşçinin yaşı (opsiyonel; 18 ve altı ile 50 ve üzeri için asgari 20 gün uygulanır)" }
          },
          "required": ["kidem_yil", "gunluk_brut_ucret"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_yillik_izin_ucreti",
        "Yıllık ücretli izin hakkını (4857 sK m.53: 1-5 yıl dahil 14 gün, 5 yıldan fazla 15 yıldan az 20 gün, 15 yıl ve fazlası 26 gün) ve kullanılmayan izin ücretini hesaplar. Ücret, çıplak günlük brüt üzerinden hesaplanır; giydirilmiş ücret esas alınmaz.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new AnnualLeavePayRequest(
            ToolArgs.Decimal(arguments, "kidem_yil") ?? -1m,
            ToolArgs.Decimal(arguments, "gunluk_brut_ucret") ?? -1m,
            ToolArgs.Decimal(arguments, "kullanilmayan_izin_gunu"),
            ToolArgs.Int(arguments, "yas"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcAnnualLeavePay(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

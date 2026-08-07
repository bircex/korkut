using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>2026 yargı harçlarını (başvurma + peşin karar-ilam + ıslah + kanun yolu + icra) kalem kalem hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class CourtFeeTool(ICalculatorService calc) : IAgentTool
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
            },
            "tahsilat_asamasi": {
              "type": "string",
              "enum": ["haciz_oncesi", "haciz_sonrasi", "satis_sonrasi"],
              "description": "icra_takip için: tahsil harcı hangi aşamada doğuyor (varsayılan haciz_oncesi)"
            },
            "tahsilat_tutari": { "type": "number", "description": "TL; icra_takip için fiilen tahsil edilen tutar, tahsil harcı hesabına dahil edilir" }
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
        var request = new CourtFeeRequest(
            ToolArgs.String(arguments, "islem") ?? "",
            ToolArgs.String(arguments, "dava_turu"),
            ToolArgs.Decimal(arguments, "dava_degeri"),
            ToolArgs.Decimal(arguments, "eski_deger"),
            ToolArgs.Decimal(arguments, "yeni_deger"),
            ToolArgs.Bool(arguments, "maktu"),
            ToolArgs.String(arguments, "mahkeme"),
            ToolArgs.String(arguments, "tahsilat_asamasi"),
            ToolArgs.Decimal(arguments, "tahsilat_tutari"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcCourtFee(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

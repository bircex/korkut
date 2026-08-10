using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>İhbar tazminatı hesaplar (4857 sK m.17 süreleri, giydirilmiş brüt, gelir + damga vergisi). Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class NoticePayTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "kidem_ay": { "type": "number", "description": "Kıdem süresi, ay olarak (tarihler verilmediyse zorunlu)" },
            "ise_giris": { "type": "string", "format": "date", "description": "İşe giriş tarihi (kidem_ay yerine tarih çifti verilebilir)" },
            "isten_cikis": { "type": "string", "format": "date", "description": "Fesih tarihi" },
            "brut_maas": { "type": "number", "description": "Son aylık brüt çıplak ücret, TL" },
            "yemek": { "type": "number", "description": "Aylık brüt yemek yardımı, TL (opsiyonel)" },
            "yol": { "type": "number", "description": "Aylık brüt yol yardımı, TL (opsiyonel)" },
            "diger": { "type": "number", "description": "Diğer düzenli aylık brüt yan haklar, TL (opsiyonel)" }
          },
          "required": ["brut_maas"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_ihbar_tazminati",
        "İhbar tazminatı hesaplar: 4857 sK m.17 bildirim süreleri (6 aydan az 2 hafta, 6-18 ay 4 hafta, 18-36 ay 6 hafta, 36 aydan fazla 8 hafta) x giydirilmiş günlük brüt ücret. İhbar tazminatı gelir vergisine ve damga vergisine tabidir (SGK primi kesilmez); gelir vergisi kesintisi yaklaşık hesaplanır.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new NoticePayRequest(
            ToolArgs.Decimal(arguments, "kidem_ay"),
            ToolArgs.Date(arguments, "ise_giris"),
            ToolArgs.Date(arguments, "isten_cikis"),
            ToolArgs.Decimal(arguments, "brut_maas") ?? -1m,
            ToolArgs.Decimal(arguments, "yemek"),
            ToolArgs.Decimal(arguments, "yol"),
            ToolArgs.Decimal(arguments, "diger"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcNoticePay(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

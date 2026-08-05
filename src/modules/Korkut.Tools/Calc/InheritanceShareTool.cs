using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Yasal miras payı ve saklı pay hesabı (TMK zümre sistemi). Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class InheritanceShareTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "es_var": { "type": "boolean", "description": "Sağ kalan eş var mı" },
            "cocuk_sayisi": { "type": "integer", "description": "Sağ çocuk sayısı (1. zümre)" },
            "anne_var": { "type": "boolean", "description": "Mirasbırakanın annesi sağ mı (2. zümre)" },
            "baba_var": { "type": "boolean", "description": "Mirasbırakanın babası sağ mı (2. zümre)" },
            "kardes_sayisi": { "type": "integer", "description": "Kardeş sayısı (ölen ana/babanın yerini alırlar, isteğe bağlı)" },
            "miras_degeri": { "type": "number", "description": "Terekenin değeri, TL (isteğe bağlı; girilirse TL karşılıkları da hesaplanır)" }
          },
          "required": ["es_var", "cocuk_sayisi", "anne_var", "baba_var"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_miras_pay",
        "TMK zümre sistemine göre yasal miras paylarını, saklı payları ve tasarruf edilebilir kısmı hesaplar (kesir, yüzde ve istenirse TL olarak). Eş + altsoy, eş + ana-baba (kardeş halefiyeti dahil) ve yalnız eş senaryolarını kapsar.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new InheritanceShareRequest(
            arguments.TryGetProperty("es_var", out var esVar) ? ToolArgs.Bool(arguments, "es_var") : null,
            ToolArgs.Int(arguments, "cocuk_sayisi") ?? -1,
            ToolArgs.Bool(arguments, "anne_var"),
            ToolArgs.Bool(arguments, "baba_var"),
            ToolArgs.Int(arguments, "kardes_sayisi"),
            ToolArgs.Decimal(arguments, "miras_degeri"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcInheritanceShare(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

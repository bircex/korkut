using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Veraset ve intikal vergisini (7338 sK, 2026 dilim ve istisnalarıyla) kademeli hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class InheritanceTaxTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "intikal_turu": {
              "type": "string",
              "enum": ["veraset", "ivazsiz"],
              "description": "veraset = miras yoluyla intikal; ivazsiz = bağış vb. karşılıksız intikal"
            },
            "matrah": { "type": "number", "description": "İntikal eden değer (TL); veraset için mirasçıya düşen hisse" },
            "es_tek_basina": {
              "type": "boolean",
              "description": "Füruğ (çocuk/torun) yoksa ve intikal eşe ise true; eş istisnası yükselir"
            }
          },
          "required": ["intikal_turu", "matrah"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_veraset_intikal",
        "Veraset ve intikal vergisini hesaplar (7338 sK, 2026 tarifesi): istisna düşer, kalan matraha kademeli dilim tablosunu uygular, efektif oranı verir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new InheritanceTaxRequest(
            ToolArgs.String(arguments, "intikal_turu") ?? "",
            ToolArgs.Decimal(arguments, "matrah") ?? -1m,
            ToolArgs.Bool(arguments, "es_tek_basina"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcInheritanceTax(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>GVK m.94/5-a işyeri kira stopajı (net/brüt) hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class RentWithholdingTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "yon": {
              "type": "string",
              "enum": ["brutten_nete", "netten_brute"],
              "description": "brutten_nete = brüt kiradan net kirayı bul; netten_brute = net kiradan brüt kirayı bul"
            },
            "tutar": { "type": "number", "description": "TL; yön'e göre brüt veya net kira tutarı" },
            "odeyen_stopaja_tabi": { "type": "boolean", "description": "Kirayı ödeyen GVK m.94 kapsamında stopaj yapmakla yükümlü bir vergi sorumlusu mu (şirket, kurum, serbest meslek erbabı vb.). Hayırsa stopaj uygulanmaz" }
          },
          "required": ["yon", "tutar", "odeyen_stopaja_tabi"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_kira_stopaji",
        "GVK m.94/5-a'ya göre işyeri kira stopajını (%20) hesaplar; ödeyen stopaj yükümlüsü değilse (örn. gerçek kişi kiracı) stopaj uygulanmaz.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new RentWithholdingRequest(
            ToolArgs.String(arguments, "yon") ?? "",
            ToolArgs.Decimal(arguments, "tutar") ?? -1m,
            ToolArgs.Bool(arguments, "odeyen_stopaja_tabi"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcRentWithholding(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

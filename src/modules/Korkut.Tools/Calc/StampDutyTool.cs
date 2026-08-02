using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Belli parayı ihtiva eden kağıtların damga vergisini (488 sK, 2026 azami sınırıyla) hesaplar.</summary>
public sealed class StampDutyTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "kagit_turu": {
              "type": "string",
              "enum": ["sozlesme", "kira_sozlesmesi", "ihale_karari", "ucret_odemesi", "fesihname"],
              "description": "sozlesme = mukavelename/taahhütname/temlikname (binde 9,48); kira_sozlesmesi binde 1,89; ihale_karari binde 5,69; ucret_odemesi binde 7,59; fesihname binde 1,89"
            },
            "tutar": { "type": "number", "description": "Kağıdın ihtiva ettiği tutar (TL); kira sözleşmesinde sözleşme süresi toplam kira bedeli" }
          },
          "required": ["kagit_turu", "tutar"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_damga_vergisi",
        "488 sayılı Kanuna göre damga vergisi hesaplar: kağıt türüne göre binde oran uygular, 2026 azami damga vergisi üst sınırını gözetir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var documentType = ToolArgs.String(arguments, "kagit_turu");
        var amount = ToolArgs.Decimal(arguments, "tutar");
        if (documentType is null || !StampDuty2026.Binde.TryGetValue(documentType, out var perMilleRate))
        {
            return Task.FromResult(ToolResult.Error(
                "kagit_turu zorunludur; geçerli değerler: " + string.Join(", ", StampDuty2026.Binde.Keys) + "."));
        }

        if (amount is not { } value || value <= 0)
        {
            return Task.FromResult(ToolResult.Error("Pozitif tutar zorunludur."));
        }

        var calculated = decimal.Round(value * perMilleRate / 1000m, 2);
        var capApplied = calculated > StampDuty2026.AzamiVergi;

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            KagitTuru = documentType,
            Tutar = value,
            BindeOran = perMilleRate,
            HesaplananVergi = calculated,
            AzamiSinir = StampDuty2026.AzamiVergi,
            AzamiUygulandi = capApplied,
            Vergi = capApplied ? StampDuty2026.AzamiVergi : calculated,
            TarifeDonemi = StampDuty2026.Donem,
            Kaynak = StampDuty2026.Kaynak,
        })));
    }
}

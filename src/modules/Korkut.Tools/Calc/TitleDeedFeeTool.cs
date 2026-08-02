using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Gayrimenkul satışında tapu harcını (alıcı + satıcı, binde 20 + binde 20) hesaplar.</summary>
public sealed class TitleDeedFeeTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "satis_bedeli": { "type": "number", "description": "Tapuda beyan edilen satış bedeli (TL); emlak vergisi değerinden düşük olamaz" },
            "taraf": {
              "type": "string",
              "enum": ["alici", "satici", "toplam"],
              "description": "Hangi tarafın harcı istendiği (varsayılan toplam)"
            }
          },
          "required": ["satis_bedeli"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_tapu_harci",
        "Gayrimenkul satışında tapu harcını hesaplar (492 sK 4 sayılı tarife I-20/a): alıcı ve satıcıdan ayrı ayrı binde 20; döner sermaye ücreti hakkında not içerir.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (ToolArgs.Decimal(arguments, "satis_bedeli") is not { } price || price <= 0)
        {
            return Task.FromResult(ToolResult.Error("Pozitif satis_bedeli zorunludur."));
        }

        var party = ToolArgs.String(arguments, "taraf") ?? "toplam";
        if (party is not ("alici" or "satici" or "toplam"))
        {
            return Task.FromResult(ToolResult.Error("taraf alici, satici veya toplam olmalıdır."));
        }

        var buyerFee = decimal.Round(price * TitleDeedFees2026.AliciOran, 2);
        var sellerFee = decimal.Round(price * TitleDeedFees2026.SaticiOran, 2);
        var fee = party switch
        {
            "alici" => buyerFee,
            "satici" => sellerFee,
            _ => buyerFee + sellerFee,
        };

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            SatisBedeli = price,
            Taraf = party,
            AliciHarci = buyerFee,
            SaticiHarci = sellerFee,
            Harc = fee,
            Not = "Harç, beyan edilen satış bedeli ile emlak vergisi değerinin yükseği üzerinden alınır. " + TitleDeedFees2026.DonerSermayeNotu,
            TarifeDonemi = TitleDeedFees2026.Donem,
            Kaynak = TitleDeedFees2026.Kaynak,
        })));
    }
}

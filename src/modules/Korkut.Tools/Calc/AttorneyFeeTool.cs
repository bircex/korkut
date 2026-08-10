using System.Text.Json;
using Korkut.Contract.Calculators;
using Korkut.Core;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>AAÜT 2025-2026'ya göre nispi veya maktu asgari vekalet ücretini hesaplar. Thin adapter over <see cref="ICalculatorService"/>.</summary>
public sealed class AttorneyFeeTool(ICalculatorService calc) : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "kategori": {
              "type": "string",
              "enum": [
                "para_davasi", "icra_takibi", "tahliyeli_icra_takibi", "icra_mahkemesi_durusmali",
                "icra_mahkemesi_durusmasiz", "sulh_hukuk", "sulh_ceza_infaz", "asliye", "tuketici",
                "fshm", "idare_vergi_durusmasiz", "idare_vergi_durusmali", "agir_ceza", "cocuk",
                "cocuk_agir_ceza", "bam_ilk_derece", "istinaf_tek_durusma", "istinaf_coklu_durusma",
                "yargitay_ilk_derece", "danistay_ilk_derece_durusmasiz", "danistay_ilk_derece_durusmali",
                "temyiz_durusmali", "uyusmazlik_mahkemesi", "aym_yuce_divan", "aym_bireysel_durusmasiz",
                "aym_bireysel_durusmali", "aym_diger_dava_isler"
              ],
              "description": "para_davasi = konusu para olan dava (nispi tarife); diğerleri maktu kalemlerdir"
            },
            "dava_degeri": { "type": "number", "description": "TL; kategori=para_davasi için zorunlu" },
            "taban_kategori": {
              "type": "string",
              "description": "Nispi hesapta taban alınacak maktu kalem (varsayılan asliye); örn. tuketici, sulh_hukuk"
            }
          },
          "required": ["kategori"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_vekalet_ucreti",
        "AAÜT 2025-2026'ya (RG 04.11.2025/33067) göre asgari vekalet ücretini hesaplar: konusu para olan davalarda kademeli nispi tarife (maktu taban kontrolüyle), diğer kategorilerde maktu ücret.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var request = new AttorneyFeeRequest(
            ToolArgs.String(arguments, "kategori") ?? "",
            ToolArgs.Decimal(arguments, "dava_degeri"),
            ToolArgs.String(arguments, "taban_kategori"));

        try
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(calc.CalcAttorneyFee(request))));
        }
        catch (BadRequestException ex)
        {
            return Task.FromResult(ToolResult.Error(ex.Message));
        }
    }
}

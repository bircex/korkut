using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>AAÜT 2025-2026'ya göre nispi veya maktu asgari vekalet ücretini hesaplar.</summary>
public sealed class AttorneyFeeTool : IAgentTool
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
        var category = ToolArgs.String(arguments, "kategori");
        if (category is null)
        {
            return Task.FromResult(ToolResult.Error("kategori zorunludur."));
        }

        if (category != "para_davasi")
        {
            if (!AttorneyFeeTariff2025To2026.Maktu.TryGetValue(category, out var fixedFee))
            {
                return Task.FromResult(ToolResult.Error(
                    "Bilinmeyen kategori. Geçerli değerler: para_davasi, " + string.Join(", ", AttorneyFeeTariff2025To2026.Maktu.Keys) + "."));
            }

            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
            {
                Kategori = category,
                UcretTuru = "maktu",
                Ucret = decimal.Round(fixedFee, 2),
                TarifeDonemi = AttorneyFeeTariff2025To2026.Donem,
                Kaynak = AttorneyFeeTariff2025To2026.Kaynak,
            })));
        }

        if (ToolArgs.Decimal(arguments, "dava_degeri") is not { } value || value <= 0)
        {
            return Task.FromResult(ToolResult.Error("kategori=para_davasi için pozitif dava_degeri zorunludur."));
        }

        var baseCategory = ToolArgs.String(arguments, "taban_kategori") ?? "asliye";
        if (!AttorneyFeeTariff2025To2026.Maktu.TryGetValue(baseCategory, out var baseFee))
        {
            return Task.FromResult(ToolResult.Error(
                "Bilinmeyen taban_kategori. Geçerli değerler: " + string.Join(", ", AttorneyFeeTariff2025To2026.Maktu.Keys) + "."));
        }

        var slices = BracketSchedule.Apply(AttorneyFeeTariff2025To2026.Nispi, value);
        var proportionalFee = decimal.Round(BracketSchedule.Total(slices), 2);
        var baseApplied = proportionalFee < baseFee;
        var breakdown = new List<object>(slices.Length);
        foreach (var s in slices)
        {
            breakdown.Add(new { Dilim = s.DilimTutari, Yuzde = s.Yuzde, Ucret = decimal.Round(s.Pay, 2) });
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Kategori = category,
            UcretTuru = "nispi",
            DavaDegeri = value,
            DilimKirilimi = breakdown,
            NispiUcret = proportionalFee,
            MaktuTaban = baseFee,
            TabanKategori = baseCategory,
            TabanUygulandi = baseApplied,
            Ucret = baseApplied ? baseFee : proportionalFee,
            Not = "Nispi ücret ilgili mahkemenin maktu ücretinin altına inemez (AAÜT m.13); taban varsayılan olarak asliye maktusudur, taban_kategori ile değiştirilebilir.",
            TarifeDonemi = AttorneyFeeTariff2025To2026.Donem,
            Kaynak = AttorneyFeeTariff2025To2026.Kaynak,
        })));
    }
}

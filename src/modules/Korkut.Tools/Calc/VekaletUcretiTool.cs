using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>AAÜT 2025-2026'ya göre nispi veya maktu asgari vekalet ücretini hesaplar.</summary>
public sealed class VekaletUcretiTool : IAgentTool
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
                "cocuk_agir_ceza", "istinaf", "yargitay_durusmasiz", "yargitay_durusmali",
                "danistay", "aym_bireysel_basvuru"
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
        var kategori = ToolArgs.String(arguments, "kategori");
        if (kategori is null)
        {
            return Task.FromResult(ToolResult.Error("kategori zorunludur."));
        }

        if (kategori != "para_davasi")
        {
            if (!Aaut20252026.Maktu.TryGetValue(kategori, out var maktu))
            {
                return Task.FromResult(ToolResult.Error(
                    "Bilinmeyen kategori. Geçerli değerler: para_davasi, " + string.Join(", ", Aaut20252026.Maktu.Keys) + "."));
            }

            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
            {
                Kategori = kategori,
                UcretTuru = "maktu",
                Ucret = decimal.Round(maktu, 2),
                TarifeDonemi = Aaut20252026.Donem,
                Kaynak = Aaut20252026.Kaynak,
            })));
        }

        if (ToolArgs.Decimal(arguments, "dava_degeri") is not { } deger || deger <= 0)
        {
            return Task.FromResult(ToolResult.Error("kategori=para_davasi için pozitif dava_degeri zorunludur."));
        }

        var tabanKategori = ToolArgs.String(arguments, "taban_kategori") ?? "asliye";
        if (!Aaut20252026.Maktu.TryGetValue(tabanKategori, out var taban))
        {
            return Task.FromResult(ToolResult.Error(
                "Bilinmeyen taban_kategori. Geçerli değerler: " + string.Join(", ", Aaut20252026.Maktu.Keys) + "."));
        }

        var slices = BracketSchedule.Apply(Aaut20252026.Nispi, deger);
        var nispi = decimal.Round(BracketSchedule.Total(slices), 2);
        var tabanUygulandi = nispi < taban;
        var kirilim = new List<object>(slices.Length);
        foreach (var s in slices)
        {
            kirilim.Add(new { Dilim = s.DilimTutari, Yuzde = s.Yuzde, Ucret = decimal.Round(s.Pay, 2) });
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Kategori = kategori,
            UcretTuru = "nispi",
            DavaDegeri = deger,
            DilimKirilimi = kirilim,
            NispiUcret = nispi,
            MaktuTaban = taban,
            TabanKategori = tabanKategori,
            TabanUygulandi = tabanUygulandi,
            Ucret = tabanUygulandi ? taban : nispi,
            Not = "Nispi ücret ilgili mahkemenin maktu ücretinin altına inemez (AAÜT m.13); taban varsayılan olarak asliye maktusudur, taban_kategori ile değiştirilebilir.",
            TarifeDonemi = Aaut20252026.Donem,
            Kaynak = Aaut20252026.Kaynak,
        })));
    }
}

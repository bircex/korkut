using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>2026 Arabuluculuk Asgari Ücret Tarifesi'ne göre saatlik veya anlaşma (nispi) ücretini hesaplar.</summary>
public sealed class ArabuluculukUcretiTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "uyusmazlik_turu": {
              "type": "string",
              "enum": ["aile", "isci_isveren", "tuketici", "kira", "ortakligin_giderilmesi", "ticari", "diger"],
              "description": "kira = kira, komşu hakkı ve kat mülkiyeti uyuşmazlıkları"
            },
            "anlasma": { "type": "boolean", "description": "Süreç anlaşmayla mı sonuçlandı" },
            "anlasma_tutari": { "type": "number", "description": "TL; anlaşma varsa üzerinde anlaşılan tutar" },
            "taraf_sayisi": { "type": "integer", "description": "Varsayılan 2" },
            "saat": { "type": "integer", "description": "Anlaşma yoksa harcanan saat sayısı" },
            "arabulucu_sayisi": { "type": "integer", "description": "Varsayılan 1; birden fazla arabulucuda anlaşma dilim yüzdeleri artar" }
          },
          "required": ["uyusmazlik_turu", "anlasma"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_arabuluculuk_ucreti",
        "2026 Arabuluculuk Asgari Ücret Tarifesi'ne (RG 26.12.2025/33119) göre ücret hesaplar: anlaşma yoksa taraf sayısına göre saatlik ücret, anlaşma varsa kademeli nispi ücret (asgari anlaşma ücreti tabanıyla).",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var tur = ToolArgs.String(arguments, "uyusmazlik_turu");
        if (tur is null || !MediationFees2026.Saatlik.TryGetValue(tur, out var saatlikTablo))
        {
            return Task.FromResult(ToolResult.Error(
                "uyusmazlik_turu zorunludur; geçerli değerler: " + string.Join(", ", MediationFees2026.Saatlik.Keys) + "."));
        }

        var tarafSayisi = ToolArgs.Int(arguments, "taraf_sayisi") ?? 2;
        if (tarafSayisi < 2)
        {
            return Task.FromResult(ToolResult.Error("taraf_sayisi en az 2 olmalıdır."));
        }

        const string DavaSartiNotu =
            "Dava şartı arabuluculukta taraflar anlaşamazsa ilk 2 saatlik ücret Adalet Bakanlığı bütçesinden ödenir; aşan kısım aksi kararlaştırılmadıkça taraflarca eşit karşılanır.";

        if (!ToolArgs.Bool(arguments, "anlasma"))
        {
            if (ToolArgs.Int(arguments, "saat") is not { } saat || saat < 1)
            {
                return Task.FromResult(ToolResult.Error("Anlaşma yoksa pozitif saat zorunludur."));
            }

            var saatlik = saatlikTablo[MediationFees2026.TarafBandi(tarafSayisi)];
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
            {
                UyusmazlikTuru = tur,
                UcretTuru = "saatlik",
                TarafSayisi = tarafSayisi,
                SaatlikUcret = decimal.Round(saatlik, 2),
                Saat = saat,
                Ucret = decimal.Round(saatlik * saat, 2),
                Not = DavaSartiNotu,
                TarifeDonemi = MediationFees2026.Donem,
                Kaynak = MediationFees2026.Kaynak,
            })));
        }

        if (ToolArgs.Decimal(arguments, "anlasma_tutari") is not { } tutar || tutar <= 0)
        {
            return Task.FromResult(ToolResult.Error("Anlaşma varsa pozitif anlasma_tutari zorunludur."));
        }

        var cokluArabulucu = (ToolArgs.Int(arguments, "arabulucu_sayisi") ?? 1) > 1;
        var tablo = cokluArabulucu ? MediationFees2026.AnlasmaCokluArabulucu : MediationFees2026.AnlasmaTekArabulucu;
        var slices = BracketSchedule.Apply(tablo, tutar);
        var nispi = decimal.Round(BracketSchedule.Total(slices), 2);
        var asgari = tur is "ticari" or "ortakligin_giderilmesi"
            ? MediationFees2026.AsgariAnlasmaTicariOrtaklik
            : MediationFees2026.AsgariAnlasmaGenel;
        var asgariUygulandi = nispi < asgari;
        var kirilim = new List<object>(slices.Length);
        foreach (var s in slices)
        {
            kirilim.Add(new { Dilim = s.DilimTutari, Yuzde = s.Yuzde, Ucret = decimal.Round(s.Pay, 2) });
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            UyusmazlikTuru = tur,
            UcretTuru = "anlasma_nispi",
            AnlasmaTutari = tutar,
            ArabulucuSayisi = cokluArabulucu ? "birden_fazla" : "tek",
            DilimKirilimi = kirilim,
            NispiUcret = nispi,
            AsgariAnlasmaUcreti = asgari,
            AsgariUygulandi = asgariUygulandi,
            Ucret = asgariUygulandi ? asgari : nispi,
            Not = DavaSartiNotu,
            TarifeDonemi = MediationFees2026.Donem,
            Kaynak = MediationFees2026.Kaynak,
        })));
    }
}

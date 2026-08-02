using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Veraset ve intikal vergisini (7338 sK, 2026 dilim ve istisnalarıyla) kademeli hesaplar.</summary>
public sealed class VerasetIntikalTool : IAgentTool
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
        var tur = ToolArgs.String(arguments, "intikal_turu");
        var matrah = ToolArgs.Decimal(arguments, "matrah");
        if (tur is not ("veraset" or "ivazsiz") || matrah is not { } m || m <= 0)
        {
            return Task.FromResult(ToolResult.Error("intikal_turu (veraset | ivazsiz) ve pozitif matrah zorunludur."));
        }

        var esTekBasina = ToolArgs.Bool(arguments, "es_tek_basina");
        var istisna = tur == "veraset"
            ? (esTekBasina ? InheritanceTax2026.IstisnaEsTekBasina : InheritanceTax2026.IstisnaFuruVeEs)
            : InheritanceTax2026.IstisnaIvazsiz;
        var tabiMatrah = m > istisna ? m - istisna : 0m;

        var tablo = tur == "veraset" ? InheritanceTax2026.Veraset : InheritanceTax2026.Ivazsiz;
        var slices = BracketSchedule.Apply(tablo, tabiMatrah);
        var vergi = decimal.Round(BracketSchedule.Total(slices), 2);
        var kirilim = new List<object>(slices.Length);
        foreach (var s in slices)
        {
            kirilim.Add(new { Dilim = s.DilimTutari, Yuzde = s.Yuzde, Vergi = decimal.Round(s.Pay, 2) });
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            IntikalTuru = tur,
            Matrah = m,
            Istisna = istisna,
            VergiyeTabiMatrah = tabiMatrah,
            DilimKirilimi = kirilim,
            Vergi = vergi,
            EfektifOranYuzde = decimal.Round(vergi / m * 100m, 2),
            Not = "Veraset istisnası mirasçı başına hisse üzerinden uygulanır. Ana, baba, eş ve çocuklardan ivazsız intikalde tarife oranlarının yarısı uygulanır (7338 sK m.16), bu araç bu indirimi otomatik uygulamaz.",
            TarifeDonemi = InheritanceTax2026.Donem,
            Kaynak = InheritanceTax2026.Kaynak,
        })));
    }
}

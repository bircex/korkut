using System.Text.Json;
using Korkut.Tools.Calc.Tariffs;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>
/// İş kazasında işçinin sürekli/geçici iş göremezlik (maddi) tazminatını mahkeme modunda hesaplar.
/// Aktüeryal yöntem bedensel tazminatla aynıdır (TRH-2010, net teknik faiz %0, aktif dönem sonu 60,
/// pasif dönem AGİ hariç net asgari ücret); tazminat işverenin kusur oranıyla çarpılır ve SGK
/// tarafından sürekli iş göremezlik geliri bağlandıysa peşin sermaye değeri düşülür.
/// </summary>
public sealed class IsKazasiTazminatiTool : IAgentTool
{
    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "cinsiyet": { "type": "string", "enum": ["erkek", "kadin"], "description": "İşçinin cinsiyeti (TRH-2010 bakiye ömrü için)" },
            "yas": { "type": "integer", "description": "Kaza tarihindeki yaş (0-100)" },
            "surekli_is_goremezlik_orani": { "type": "number", "description": "Sürekli iş göremezlik (maluliyet) oranı, yüzde (0-100)" },
            "isveren_kusur_orani": { "type": "number", "description": "İşverenin kusur oranı, yüzde (0-100); tazminat bu oranla çarpılır. Verilmezse 100 alınır" },
            "aylik_net_gelir": { "type": "number", "description": "İşçinin aylık net ücreti (TL); verilmezse AGİ hariç net asgari ücret alınır (2026: 28.075,50 TL)" },
            "gecici_is_goremezlik_ay": { "type": "number", "description": "Geçici iş göremezlik (istirahat) süresi, ay; bu dönem %100 iş göremezlikle hesaplanır" },
            "sgk_gelir_baglandi": { "type": "boolean", "description": "SGK sürekli iş göremezlik geliri bağladı mı" },
            "sgk_pesin_sermaye": { "type": "number", "description": "SGK gelirinin peşin sermaye değeri (TL); verilirse tazminattan düşülür" }
          },
          "required": ["cinsiyet", "yas", "surekli_is_goremezlik_orani"]
        }
        """);

    public ToolSpec Spec => new(
        "calc_is_kazasi_tazminati",
        "İş kazasında işçinin maddi (sürekli/geçici iş göremezlik) tazminatını mahkeme modunda hesaplar: TRH-2010 bakiye ömür, progresif rant (net teknik faiz %0), aktif dönem 60 yaşa kadar, pasif dönem AGİ hariç net asgari ücret; tazminat işverenin kusur oranıyla çarpılır, SGK peşin sermaye değeri verilirse düşülür.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        var cinsiyet = Trh2010.NormalizeCinsiyet(ToolArgs.String(arguments, "cinsiyet"));
        var yas = ToolArgs.Int(arguments, "yas");
        var oran = ToolArgs.Decimal(arguments, "surekli_is_goremezlik_orani");
        if (cinsiyet is null || yas is not { } y || oran is not { } m || m <= 0 || m > 100)
        {
            return Task.FromResult(ToolResult.Error(
                "cinsiyet (erkek|kadin), yas ve 0-100 arası surekli_is_goremezlik_orani zorunludur."));
        }

        var isverenKusur = ToolArgs.Decimal(arguments, "isveren_kusur_orani") ?? 100m;
        if (isverenKusur is <= 0 or > 100)
        {
            return Task.FromResult(ToolResult.Error("isveren_kusur_orani 0'dan büyük ve en çok 100 olmalıdır."));
        }

        var geciciAy = ToolArgs.Decimal(arguments, "gecici_is_goremezlik_ay") ?? 0m;
        if (geciciAy < 0)
        {
            return Task.FromResult(ToolResult.Error("gecici_is_goremezlik_ay negatif olamaz."));
        }

        var aylikGelir = ToolArgs.Decimal(arguments, "aylik_net_gelir") ?? MinimumWage.GuncelAylikNet;
        if (aylikGelir <= 0)
        {
            return Task.FromResult(ToolResult.Error("aylik_net_gelir pozitif olmalıdır."));
        }

        var pesinSermaye = ToolArgs.Decimal(arguments, "sgk_pesin_sermaye");
        if (pesinSermaye is { } ps && ps < 0)
        {
            return Task.FromResult(ToolResult.Error("sgk_pesin_sermaye negatif olamaz."));
        }

        if (ActuarialMethod.Hesapla(cinsiyet, y) is not { } donem)
        {
            return Task.FromResult(ToolResult.Error("yas 0-100 aralığında olmalıdır."));
        }

        var maluliyetOran = m / 100m;
        var yillikGelir = aylikGelir * 12m;

        var aktifKayip = decimal.Round(yillikGelir * donem.AktifYil * maluliyetOran, 2);
        var pasifKayip = decimal.Round(MinimumWage.GuncelYillikNet * donem.PasifYil * maluliyetOran, 2);
        var geciciKayip = decimal.Round(aylikGelir * geciciAy, 2);
        var brutToplam = aktifKayip + pasifKayip + geciciKayip;
        var kusurluTazminat = decimal.Round(brutToplam * (isverenKusur / 100m), 2);
        var sgkIndirimi = pesinSermaye is { } sermaye ? decimal.Round(Math.Min(sermaye, kusurluTazminat), 2) : 0m;
        var netTazminat = kusurluTazminat - sgkIndirimi;

        var varsayimlar = new List<string>
        {
            "Bakiye ömür TRH-2010 tablosundan, cinsiyet ve kaza yaşına göre alındı.",
            "Mahkeme modu progresif rant: %10 artırım + %10 iskonto birbirini götürür, net teknik faiz %0; toplam kayıp = yıllık kayıp x yıl.",
            $"Aktif dönem kaza yaşından {ActuarialMethod.AktifDonemSonuYas} yaşına kadardır; {ActuarialMethod.AktifDonemSonuYas} ve üzeri yaşta aktif dönem 0 alınır (çalışmanın sürdüğü belgelenirse hesap değişir).",
            $"Pasif dönem geliri AGİ hariç net asgari ücrettir ({MinimumWage.GuncelAylikNet:N2} TL/ay).",
            "Geçici iş göremezlik döneminde %100 iş göremezlik ve işçinin kendi ücreti esas alındı.",
            "Tazminat işverenin kusur oranıyla çarpılır; işçinin müterafik kusuru ve kaçınılmazlık indirimi bu orana yansıtılmalıdır.",
        };
        if (ToolArgs.Bool(arguments, "sgk_gelir_baglandi") && pesinSermaye is null)
        {
            varsayimlar.Add(
                "SGK sürekli iş göremezlik geliri bağlandığı bildirildi ancak peşin sermaye değeri verilmedi: SGK'nın bildireceği ilk peşin sermaye değeri tazminattan ayrıca düşülmelidir; buradaki sonuç indirimsizdir.");
        }

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Yontem = "İş kazası maddi tazminatı, mahkeme modu (TRH-2010 + progresif rant %0 net teknik faiz)",
            BakiyeOmurYil = decimal.Round(donem.BakiyeOmur, 2),
            AktifDonemYil = decimal.Round(donem.AktifYil, 2),
            PasifDonemYil = decimal.Round(donem.PasifYil, 2),
            EsasAlinanAylikNetGelir = decimal.Round(aylikGelir, 2),
            AktifDonemKaybi = aktifKayip,
            PasifDonemKaybi = pasifKayip,
            GeciciIsGoremezlikKaybi = geciciKayip,
            BrutToplam = brutToplam,
            IsverenKusurOrani = isverenKusur,
            KusurSonrasiTazminat = kusurluTazminat,
            SgkPesinSermayeIndirimi = sgkIndirimi,
            NetTazminat = netTazminat,
            Uyari = "Bilirkişi hesabı ücret artışları, belgeli çalışma süresi ve SGK peşin sermaye güncellemeleriyle bu sonuçtan sapabilir; sonuç yaklaşık değerdir.",
            Varsayimlar = varsayimlar,
            Kaynak = $"{Trh2010.Kaynak}; {MinimumWage.Kaynak}",
        })));
    }
}

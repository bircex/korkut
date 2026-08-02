using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Yasal miras payı ve saklı pay hesabı (TMK zümre sistemi).</summary>
public sealed class MirasPayTool : IAgentTool
{
    private const string Kaynak =
        "TMK m.495-501 (zümre sistemi ve yasal mirasçılar), m.499 (sağ kalan eşin payı), m.505-506 (saklı pay oranları), m.501 (mirasçı yoksa Devlet)";

    private static readonly JsonElement Schema = ToolSchema.Parse(
        """
        {
          "type": "object",
          "properties": {
            "es_var": { "type": "boolean", "description": "Sağ kalan eş var mı" },
            "cocuk_sayisi": { "type": "integer", "description": "Sağ çocuk sayısı (1. zümre)" },
            "anne_var": { "type": "boolean", "description": "Mirasbırakanın annesi sağ mı (2. zümre)" },
            "baba_var": { "type": "boolean", "description": "Mirasbırakanın babası sağ mı (2. zümre)" },
            "kardes_sayisi": { "type": "integer", "description": "Kardeş sayısı (ölen ana/babanın yerini alırlar, isteğe bağlı)" },
            "miras_degeri": { "type": "number", "description": "Terekenin değeri, TL (isteğe bağlı; girilirse TL karşılıkları da hesaplanır)" }
          },
          "required": ["es_var", "cocuk_sayisi", "anne_var", "baba_var"]
        }
        """);

    /// <summary>Sadeleştirilmiş kesir (pay/payda).</summary>
    private readonly record struct Kesir(long Pay, long Payda)
    {
        public static readonly Kesir Sifir = new(0, 1);

        public static readonly Kesir Tam = new(1, 1);

        public static Kesir Olustur(long pay, long payda)
        {
            var bolen = Obeb(Math.Abs(pay), payda);
            return bolen == 0 ? Sifir : new Kesir(pay / bolen, payda / bolen);
        }

        public static Kesir Carp(Kesir a, Kesir b) => Olustur(a.Pay * b.Pay, a.Payda * b.Payda);

        public static Kesir Topla(Kesir a, Kesir b) => Olustur((a.Pay * b.Payda) + (b.Pay * a.Payda), a.Payda * b.Payda);

        public static Kesir Cikar(Kesir a, Kesir b) => Olustur((a.Pay * b.Payda) - (b.Pay * a.Payda), a.Payda * b.Payda);

        public static Kesir Bol(Kesir a, long sayi) => Olustur(a.Pay, a.Payda * sayi);

        public decimal Yuzde => decimal.Round(Pay * 100m / Payda, 2);

        public string Metin => Pay == 0 ? "0" : Payda == 1 ? $"{Pay}" : $"{Pay}/{Payda}";

        public decimal? Tutar(decimal? deger) => deger is { } d ? decimal.Round(d * Pay / Payda, 2) : null;

        private static long Obeb(long a, long b)
        {
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }

            return a;
        }
    }

    private sealed record Mirasci(string Rol, int KisiSayisi, Kesir ToplamPay, Kesir SakliPay);

    public ToolSpec Spec => new(
        "calc_miras_pay",
        "TMK zümre sistemine göre yasal miras paylarını, saklı payları ve tasarruf edilebilir kısmı hesaplar (kesir, yüzde ve istenirse TL olarak). Eş + altsoy, eş + ana-baba (kardeş halefiyeti dahil) ve yalnız eş senaryolarını kapsar.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("es_var", out _) || ToolArgs.Int(arguments, "cocuk_sayisi") is not { } cocukSayisi || cocukSayisi < 0)
        {
            return Task.FromResult(ToolResult.Error("es_var ve negatif olmayan cocuk_sayisi zorunludur."));
        }

        var esVar = ToolArgs.Bool(arguments, "es_var");
        var anneVar = ToolArgs.Bool(arguments, "anne_var");
        var babaVar = ToolArgs.Bool(arguments, "baba_var");
        var kardesSayisi = ToolArgs.Int(arguments, "kardes_sayisi") ?? 0;
        if (kardesSayisi < 0)
        {
            return Task.FromResult(ToolResult.Error("kardes_sayisi negatif olamaz."));
        }

        var deger = ToolArgs.Decimal(arguments, "miras_degeri");
        var uyarilar = new List<string>();
        var mirascilar = new List<Mirasci>();

        if (cocukSayisi > 0)
        {
            // 1. zümre: altsoy; eş varsa 1/4 alır (TMK m.499/1), kalan altsoyundur.
            var altsoyToplam = Kesir.Tam;
            if (esVar)
            {
                var esPay = Kesir.Olustur(1, 4);
                mirascilar.Add(new Mirasci("es", 1, esPay, esPay)); // eşin saklı payı yasal payının tamamı (TMK m.506/4)
                altsoyToplam = Kesir.Olustur(3, 4);
            }

            mirascilar.Add(new Mirasci("cocuklar", cocukSayisi, altsoyToplam, Kesir.Carp(altsoyToplam, Kesir.Olustur(1, 2))));
            uyarilar.Add("Mirasbırakandan önce ölen çocuğun yerini kendi altsoyu (torunlar) alır; bu halefiyet burada modellenmedi.");
        }
        else if (anneVar || babaVar || kardesSayisi > 0)
        {
            // 2. zümre: ana-baba; eş varsa 1/2 alır (TMK m.499/2). Ölen ana/babanın yerini kardeşler alır;
            // bir kanat tamamen boşsa payı eş varken eşe, eş yokken dolu kanata geçer (TMK m.496, m.499).
            var esPay = esVar ? Kesir.Olustur(1, 2) : Kesir.Sifir;
            var kanat = esVar ? Kesir.Olustur(1, 4) : Kesir.Olustur(1, 2);
            var annePay = Kesir.Sifir;
            var babaPay = Kesir.Sifir;
            var kardesPay = Kesir.Sifir;
            var bosKanat = Kesir.Sifir;

            if (anneVar)
            {
                annePay = kanat;
            }
            else if (kardesSayisi > 0)
            {
                kardesPay = Kesir.Topla(kardesPay, kanat);
            }
            else
            {
                bosKanat = Kesir.Topla(bosKanat, kanat);
            }

            if (babaVar)
            {
                babaPay = kanat;
            }
            else if (kardesSayisi > 0)
            {
                kardesPay = Kesir.Topla(kardesPay, kanat);
            }
            else
            {
                bosKanat = Kesir.Topla(bosKanat, kanat);
            }

            if (bosKanat.Pay > 0)
            {
                if (esVar)
                {
                    esPay = Kesir.Topla(esPay, bosKanat);
                }
                else if (annePay.Pay > 0)
                {
                    annePay = Kesir.Topla(annePay, bosKanat);
                }
                else if (babaPay.Pay > 0)
                {
                    babaPay = Kesir.Topla(babaPay, bosKanat);
                }
                else
                {
                    kardesPay = Kesir.Topla(kardesPay, bosKanat);
                }
            }

            if (esVar)
            {
                mirascilar.Add(new Mirasci("es", 1, esPay, esPay)); // ana-baba zümresiyle birlikte saklı pay yasal payın tamamı
            }

            if (annePay.Pay > 0)
            {
                mirascilar.Add(new Mirasci("anne", 1, annePay, Kesir.Carp(annePay, Kesir.Olustur(1, 4))));
            }

            if (babaPay.Pay > 0)
            {
                mirascilar.Add(new Mirasci("baba", 1, babaPay, Kesir.Carp(babaPay, Kesir.Olustur(1, 4))));
            }

            if (kardesPay.Pay > 0)
            {
                mirascilar.Add(new Mirasci("kardesler", kardesSayisi, kardesPay, Kesir.Sifir));
                uyarilar.Add("Kardeşlerin saklı payı yoktur (2007'de kaldırıldı); anne ve babanın saklı payı yasal paylarının 1/4'üdür (TMK m.506).");
            }
        }
        else if (esVar)
        {
            // 1. ve 2. zümrede mirasçı yok: girdiler büyük ana-babayı kapsamadığından eş tek mirasçı kabul edildi.
            mirascilar.Add(new Mirasci("es", 1, Kesir.Tam, Kesir.Olustur(3, 4)));
            uyarilar.Add("Büyük ana-baba veya onların altsoyu (3. zümre) sağ ise eşin yasal payı 3/4'e iner (TMK m.499/3); bu girdilerle 3. zümre sorgulanamadığından eş tek mirasçı varsayıldı. Tek başına yasal mirasçı olan eşin saklı payı yasal payının 3/4'üdür (TMK m.506/4).");
        }
        else
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
            {
                Mirascilar = Array.Empty<object>(),
                Aciklama = "Girilen bilgilere göre yasal mirasçı yok; mirasçı bırakmadan ölen kimsenin mirası Devlete geçer (TMK m.501).",
                Kaynak = Kaynak,
            })));
        }

        var sakliToplam = Kesir.Sifir;
        var payToplam = Kesir.Sifir;
        var cikti = new List<object>(mirascilar.Count);
        foreach (var m in mirascilar)
        {
            payToplam = Kesir.Topla(payToplam, m.ToplamPay);
            sakliToplam = Kesir.Topla(sakliToplam, m.SakliPay);
            var kisiBasi = m.KisiSayisi > 1 ? Kesir.Bol(m.ToplamPay, m.KisiSayisi) : m.ToplamPay;
            cikti.Add(new
            {
                Rol = m.Rol,
                KisiSayisi = m.KisiSayisi,
                ToplamPay = m.ToplamPay.Metin,
                KisiBasiPay = kisiBasi.Metin,
                Yuzde = m.ToplamPay.Yuzde,
                ToplamTutarTl = m.ToplamPay.Tutar(deger),
                KisiBasiTutarTl = kisiBasi.Tutar(deger),
                SakliPay = m.SakliPay.Metin,
                SakliPayTutarTl = m.SakliPay.Tutar(deger),
            });
        }

        var tasarruf = Kesir.Cikar(Kesir.Tam, sakliToplam);
        uyarilar.Add(
            "Hesap sadeleştirilmiş bir zümre modelidir: evlatlık, mal rejimi tasfiyesi (eşin katılma alacağı terekeden önce ayrılır), mirastan feragat/yoksunluk ve vasiyetnameler dikkate alınmadı; kesin paylaşım için tereke dosyası incelenmelidir.");

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Mirascilar = cikti,
            PayToplami = payToplam.Metin,
            SakliPayToplami = sakliToplam.Metin,
            TasarrufEdilebilirKisim = tasarruf.Metin,
            TasarrufEdilebilirYuzde = tasarruf.Yuzde,
            TasarrufEdilebilirTutarTl = tasarruf.Tutar(deger),
            Uyarilar = uyarilar,
            Kaynak = Kaynak,
        })));
    }
}

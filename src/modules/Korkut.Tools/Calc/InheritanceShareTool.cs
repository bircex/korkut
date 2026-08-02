using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Calc;

/// <summary>Yasal miras payı ve saklı pay hesabı (TMK zümre sistemi).</summary>
public sealed class InheritanceShareTool : IAgentTool
{
    private const string Source =
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
    private readonly record struct Fraction(long Numerator, long Denominator)
    {
        public static readonly Fraction Zero = new(0, 1);

        public static readonly Fraction One = new(1, 1);

        public static Fraction Olustur(long numerator, long denominator)
        {
            var divisor = Obeb(Math.Abs(numerator), denominator);
            return divisor == 0 ? Zero : new Fraction(numerator / divisor, denominator / divisor);
        }

        public static Fraction Carp(Fraction a, Fraction b) => Olustur(a.Numerator * b.Numerator, a.Denominator * b.Denominator);

        public static Fraction Topla(Fraction a, Fraction b) => Olustur((a.Numerator * b.Denominator) + (b.Numerator * a.Denominator), a.Denominator * b.Denominator);

        public static Fraction Cikar(Fraction a, Fraction b) => Olustur((a.Numerator * b.Denominator) - (b.Numerator * a.Denominator), a.Denominator * b.Denominator);

        public static Fraction Bol(Fraction a, long count) => Olustur(a.Numerator, a.Denominator * count);

        public decimal Percentage => decimal.Round(Numerator * 100m / Denominator, 2);

        public string Text => Numerator == 0 ? "0" : Denominator == 1 ? $"{Numerator}" : $"{Numerator}/{Denominator}";

        public decimal? Tutar(decimal? value) => value is { } v ? decimal.Round(v * Numerator / Denominator, 2) : null;

        private static long Obeb(long a, long b)
        {
            while (b != 0)
            {
                (a, b) = (b, a % b);
            }

            return a;
        }
    }

    private sealed record Heir(string Role, int PersonCount, Fraction TotalShare, Fraction ReservedShare);

    public ToolSpec Spec => new(
        "calc_miras_pay",
        "TMK zümre sistemine göre yasal miras paylarını, saklı payları ve tasarruf edilebilir kısmı hesaplar (kesir, yüzde ve istenirse TL olarak). Eş + altsoy, eş + ana-baba (kardeş halefiyeti dahil) ve yalnız eş senaryolarını kapsar.",
        Schema);

    public string? RequiresKey => null;

    public string? RequiredPermission => null;

    public Task<ToolResult> InvokeAsync(JsonElement arguments, AgentToolContext context, CancellationToken ct = default)
    {
        if (!arguments.TryGetProperty("es_var", out _) || ToolArgs.Int(arguments, "cocuk_sayisi") is not { } childCount || childCount < 0)
        {
            return Task.FromResult(ToolResult.Error("es_var ve negatif olmayan cocuk_sayisi zorunludur."));
        }

        var hasSpouse = ToolArgs.Bool(arguments, "es_var");
        var hasMother = ToolArgs.Bool(arguments, "anne_var");
        var hasFather = ToolArgs.Bool(arguments, "baba_var");
        var siblingCount = ToolArgs.Int(arguments, "kardes_sayisi") ?? 0;
        if (siblingCount < 0)
        {
            return Task.FromResult(ToolResult.Error("kardes_sayisi negatif olamaz."));
        }

        var estateValue = ToolArgs.Decimal(arguments, "miras_degeri");
        var warnings = new List<string>();
        var heirs = new List<Heir>();

        if (childCount > 0)
        {
            // 1. zümre: altsoy; eş varsa 1/4 alır (TMK m.499/1), kalan altsoyundur.
            var descendantsTotal = Fraction.One;
            if (hasSpouse)
            {
                var spouseShare = Fraction.Olustur(1, 4);
                heirs.Add(new Heir("es", 1, spouseShare, spouseShare)); // eşin saklı payı yasal payının tamamı (TMK m.506/4)
                descendantsTotal = Fraction.Olustur(3, 4);
            }

            heirs.Add(new Heir("cocuklar", childCount, descendantsTotal, Fraction.Carp(descendantsTotal, Fraction.Olustur(1, 2))));
            warnings.Add("Mirasbırakandan önce ölen çocuğun yerini kendi altsoyu (torunlar) alır; bu halefiyet burada modellenmedi.");
        }
        else if (hasMother || hasFather || siblingCount > 0)
        {
            // 2. zümre: ana-baba; eş varsa 1/2 alır (TMK m.499/2). Ölen ana/babanın yerini kardeşler alır;
            // bir kanat tamamen boşsa payı eş varken eşe, eş yokken dolu kanata geçer (TMK m.496, m.499).
            var spouseShare = hasSpouse ? Fraction.Olustur(1, 2) : Fraction.Zero;
            var branch = hasSpouse ? Fraction.Olustur(1, 4) : Fraction.Olustur(1, 2);
            var motherShare = Fraction.Zero;
            var fatherShare = Fraction.Zero;
            var siblingShare = Fraction.Zero;
            var emptyBranch = Fraction.Zero;

            if (hasMother)
            {
                motherShare = branch;
            }
            else if (siblingCount > 0)
            {
                siblingShare = Fraction.Topla(siblingShare, branch);
            }
            else
            {
                emptyBranch = Fraction.Topla(emptyBranch, branch);
            }

            if (hasFather)
            {
                fatherShare = branch;
            }
            else if (siblingCount > 0)
            {
                siblingShare = Fraction.Topla(siblingShare, branch);
            }
            else
            {
                emptyBranch = Fraction.Topla(emptyBranch, branch);
            }

            if (emptyBranch.Numerator > 0)
            {
                if (hasSpouse)
                {
                    spouseShare = Fraction.Topla(spouseShare, emptyBranch);
                }
                else if (motherShare.Numerator > 0)
                {
                    motherShare = Fraction.Topla(motherShare, emptyBranch);
                }
                else if (fatherShare.Numerator > 0)
                {
                    fatherShare = Fraction.Topla(fatherShare, emptyBranch);
                }
                else
                {
                    siblingShare = Fraction.Topla(siblingShare, emptyBranch);
                }
            }

            if (hasSpouse)
            {
                heirs.Add(new Heir("es", 1, spouseShare, spouseShare)); // ana-baba zümresiyle birlikte saklı pay yasal payın tamamı
            }

            if (motherShare.Numerator > 0)
            {
                heirs.Add(new Heir("anne", 1, motherShare, Fraction.Carp(motherShare, Fraction.Olustur(1, 4))));
            }

            if (fatherShare.Numerator > 0)
            {
                heirs.Add(new Heir("baba", 1, fatherShare, Fraction.Carp(fatherShare, Fraction.Olustur(1, 4))));
            }

            if (siblingShare.Numerator > 0)
            {
                heirs.Add(new Heir("kardesler", siblingCount, siblingShare, Fraction.Zero));
                warnings.Add("Kardeşlerin saklı payı yoktur (2007'de kaldırıldı); anne ve babanın saklı payı yasal paylarının 1/4'üdür (TMK m.506).");
            }
        }
        else if (hasSpouse)
        {
            // 1. ve 2. zümrede mirasçı yok: girdiler büyük ana-babayı kapsamadığından eş tek mirasçı kabul edildi.
            heirs.Add(new Heir("es", 1, Fraction.One, Fraction.Olustur(3, 4)));
            warnings.Add("Büyük ana-baba veya onların altsoyu (3. zümre) sağ ise eşin yasal payı 3/4'e iner (TMK m.499/3); bu girdilerle 3. zümre sorgulanamadığından eş tek mirasçı varsayıldı. Tek başına yasal mirasçı olan eşin saklı payı yasal payının 3/4'üdür (TMK m.506/4).");
        }
        else
        {
            return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
            {
                Mirascilar = Array.Empty<object>(),
                Aciklama = "Girilen bilgilere göre yasal mirasçı yok; mirasçı bırakmadan ölen kimsenin mirası Devlete geçer (TMK m.501).",
                Kaynak = Source,
            })));
        }

        var reservedTotal = Fraction.Zero;
        var shareTotal = Fraction.Zero;
        var output = new List<object>(heirs.Count);
        foreach (var heir in heirs)
        {
            shareTotal = Fraction.Topla(shareTotal, heir.TotalShare);
            reservedTotal = Fraction.Topla(reservedTotal, heir.ReservedShare);
            var perPerson = heir.PersonCount > 1 ? Fraction.Bol(heir.TotalShare, heir.PersonCount) : heir.TotalShare;
            output.Add(new
            {
                Rol = heir.Role,
                KisiSayisi = heir.PersonCount,
                ToplamPay = heir.TotalShare.Text,
                KisiBasiPay = perPerson.Text,
                Yuzde = heir.TotalShare.Percentage,
                ToplamTutarTl = heir.TotalShare.Tutar(estateValue),
                KisiBasiTutarTl = perPerson.Tutar(estateValue),
                SakliPay = heir.ReservedShare.Text,
                SakliPayTutarTl = heir.ReservedShare.Tutar(estateValue),
            });
        }

        var disposable = Fraction.Cikar(Fraction.One, reservedTotal);
        warnings.Add(
            "Hesap sadeleştirilmiş bir zümre modelidir: evlatlık, mal rejimi tasfiyesi (eşin katılma alacağı terekeden önce ayrılır), mirastan feragat/yoksunluk ve vasiyetnameler dikkate alınmadı; kesin paylaşım için tereke dosyası incelenmelidir.");

        return Task.FromResult(ToolResult.Ok(ToolArgs.Result(new
        {
            Mirascilar = output,
            PayToplami = shareTotal.Text,
            SakliPayToplami = reservedTotal.Text,
            TasarrufEdilebilirKisim = disposable.Text,
            TasarrufEdilebilirYuzde = disposable.Percentage,
            TasarrufEdilebilirTutarTl = disposable.Tutar(estateValue),
            Uyarilar = warnings,
            Kaynak = Source,
        })));
    }
}

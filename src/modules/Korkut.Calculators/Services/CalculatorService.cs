using Korkut.Calculators.Services.Rates;
using Korkut.Calculators.Services.Tariffs;
using Korkut.Contract.Calculators;
using Korkut.Core;

namespace Korkut.Calculators.Services;

/// <summary>
/// The 27 legal/financial calculators, ported from the agent tools of the same name
/// (Korkut.Tools/Calc/*Tool.cs) with the computation logic kept verbatim — only the input
/// (JsonElement args -&gt; typed Request) and output (anonymous object -&gt; typed Response) shells
/// changed, plus validation-failure returns becoming <see cref="BadRequestException"/> throws.
/// </summary>
internal sealed class CalculatorService(TcmbRatesClient tcmb) : ICalculatorService
{
    // ---- calc_nafaka ----

    public AlimonyResponse CalcAlimony(AlimonyRequest request)
    {
        const string source =
            "TMK m.169 (tedbir nafakası), m.175 (yoksulluk nafakası), m.182 ve m.330 (iştirak nafakası); oran bantları yerleşik uygulamadan alınmış tahminlerdir, kanuni bir formül yoktur";
        const decimal povertyLower = 0.20m;
        const decimal povertyUpper = 0.25m;
        const decimal childSupportPerChildLower = 0.15m;
        const decimal childSupportPerChildUpper = 0.20m;
        const decimal totalCap = 0.50m;

        var alimonyType = request.NafakaTuru;
        var income = request.YukumluNetGelir;
        if (alimonyType is not ("yoksulluk" or "istirak" or "tedbir") || income <= 0)
        {
            throw new BadRequestException("Geçerli nafaka_turu ve pozitif yukumlu_net_gelir zorunludur.");
        }

        var warnings = new List<string>();
        decimal lowerRatio;
        decimal upperRatio;
        string bandDescription;

        if (alimonyType == "istirak")
        {
            if (request.CocukSayisi is not { } childCount || childCount <= 0)
            {
                throw new BadRequestException("İştirak nafakası için pozitif cocuk_sayisi zorunludur.");
            }

            lowerRatio = childSupportPerChildLower * childCount;
            upperRatio = childSupportPerChildUpper * childCount;
            bandDescription = $"Çocuk başına %{childSupportPerChildLower * 100:0}-{childSupportPerChildUpper * 100:0} bandı x {childCount} çocuk";
            warnings.Add("İştirak nafakası çocuğun yaşı, eğitim gideri ve tarafların mali gücüne göre belirlenir; çocuk büyüdükçe artırım davası açılabilir (TMK m.331).");
        }
        else
        {
            lowerRatio = povertyLower;
            upperRatio = povertyUpper;
            bandDescription = $"Gelirin %{povertyLower * 100:0}-{povertyUpper * 100:0} bandı";
            if (alimonyType == "yoksulluk")
            {
                warnings.Add("Yoksulluk nafakası için talep edenin boşanma yüzünden yoksulluğa düşecek olması ve kusurunun daha ağır olmaması gerekir (TMK m.175).");
                if (request.EsCalisiyor)
                {
                    warnings.Add("Talep eden eş çalışıyor: düzenli ve yeterli geliri varsa yoksulluk koşulu oluşmayabilir ve nafaka hiç takdir edilmeyebilir.");
                }
            }
            else
            {
                warnings.Add("Tedbir nafakası dava süresince geçerlidir ve kusur aranmaz (TMK m.169); müşterek çocuklar için ayrıca çocuk başına %15-20 bandında tedbir nafakası eklenebilir.");
            }
        }

        var capApplied = false;
        if (upperRatio > totalCap)
        {
            upperRatio = totalCap;
            capApplied = true;
        }

        if (lowerRatio > totalCap)
        {
            lowerRatio = totalCap;
            capApplied = true;
        }

        if (capApplied)
        {
            warnings.Add($"Toplam nafakaların gelirin yaklaşık %{totalCap * 100:0}'sini aşmaması yönündeki uygulama gözetilerek aralık tavandan kırpıldı.");
        }

        warnings.Add(
            "BU BİR TAHMİNDİR, HUKUKİ DAYANAK DEĞİLDİR: nafakada yerleşik bir formül yoktur; tutar, tarafların sosyoekonomik durumu, ihtiyaçlar ve hakkaniyet gözetilerek tamamen hakimin takdiriyle belirlenir. Emsal kararlar ve tarafların yaşam düzeyi somut dosyada incelenmelidir.");

        return new AlimonyResponse(
            alimonyType, income, bandDescription, decimal.Round(income * lowerRatio, 2),
            decimal.Round(income * upperRatio, 2), warnings, source);
    }

    // ---- calc_yillik_izin_ucreti ----

    public AnnualLeavePayResponse CalcAnnualLeavePay(AnnualLeavePayRequest request)
    {
        var seniorityYears = request.KidemYil;
        var dailyWage = request.GunlukBrutUcret;
        if (dailyWage <= 0)
        {
            throw new BadRequestException("kidem_yil ve pozitif gunluk_brut_ucret zorunludur.");
        }

        if (seniorityYears < 1)
        {
            throw new BadRequestException(
                "Yıllık ücretli izin hakkı, en az 1 yıl çalışmayla doğar (4857 sK m.53). Bu kıdemle izin hakkı henüz doğmamıştır.");
        }

        var entitlementDays = seniorityYears <= 5 ? 14 : seniorityYears < 15 ? 20 : 26;

        var minimumAgeRuleApplied = request.Yas is { } age && (age <= 18 || age >= 50);
        if (minimumAgeRuleApplied && entitlementDays < 20)
        {
            entitlementDays = 20;
        }

        var calculatedDays = request.KullanilmayanIzinGunu ?? entitlementDays;
        if (calculatedDays < 0)
        {
            throw new BadRequestException("kullanilmayan_izin_gunu negatif olamaz.");
        }

        var grossPay = decimal.Round(calculatedDays * dailyWage, 2);
        return new AnnualLeavePayResponse(
            seniorityYears, entitlementDays, minimumAgeRuleApplied, calculatedDays, dailyWage, grossPay,
            "Yıllık izin ücreti çıplak brüt ücret üzerinden hesaplanır (giydirilmiş ücret esas alınmaz, 4857 sK m.57). Sözleşme sona erdiğinde kullanılmayan izin ücreti gelir vergisi, damga vergisi ve SGK kesintilerine tabidir; tutar brüttür.",
            "4857 sK m.53 (izin süreleri), m.57 (izin ücreti) ve m.59 (sözleşme sonunda izin alacağı)");
    }

    // ---- calc_vekalet_ucreti ----

    public AttorneyFeeResponse CalcAttorneyFee(AttorneyFeeRequest request)
    {
        var category = request.Kategori;
        if (category != "para_davasi")
        {
            if (!AttorneyFeeTariff2025To2026.Maktu.TryGetValue(category, out var fixedFee))
            {
                throw new BadRequestException(
                    "Bilinmeyen kategori. Geçerli değerler: para_davasi, " + string.Join(", ", AttorneyFeeTariff2025To2026.Maktu.Keys) + ".");
            }

            return new AttorneyFeeResponse(
                category, "maktu", null, null, null, decimal.Round(fixedFee, 2), null, null, decimal.Round(fixedFee, 2),
                null, AttorneyFeeTariff2025To2026.Donem, AttorneyFeeTariff2025To2026.Kaynak);
        }

        if (request.DavaDegeri is not { } value || value <= 0)
        {
            throw new BadRequestException("kategori=para_davasi için pozitif dava_degeri zorunludur.");
        }

        var baseCategory = request.TabanKategori ?? "asliye";
        if (!AttorneyFeeTariff2025To2026.Maktu.TryGetValue(baseCategory, out var baseFee))
        {
            throw new BadRequestException(
                "Bilinmeyen taban_kategori. Geçerli değerler: " + string.Join(", ", AttorneyFeeTariff2025To2026.Maktu.Keys) + ".");
        }

        var slices = BracketSchedule.Apply(AttorneyFeeTariff2025To2026.Nispi, value);
        var proportionalFee = decimal.Round(BracketSchedule.Total(slices), 2);
        var baseApplied = proportionalFee < baseFee;
        var breakdown = slices.Select(s => new AttorneyFeeBracketItem(s.DilimTutari, s.Yuzde, decimal.Round(s.Pay, 2))).ToList();

        return new AttorneyFeeResponse(
            category, "nispi", value, breakdown, proportionalFee, baseFee, baseCategory, baseApplied,
            baseApplied ? baseFee : proportionalFee,
            "Nispi ücret ilgili mahkemenin maktu ücretinin altına inemez (AAÜT m.13); taban varsayılan olarak asliye maktusudur, taban_kategori ile değiştirilebilir.",
            AttorneyFeeTariff2025To2026.Donem, AttorneyFeeTariff2025To2026.Kaynak);
    }

    // ---- calc_bedensel_tazminat ----

    public BodilyInjuryCompensationResponse CalcBodilyInjuryCompensation(BodilyInjuryCompensationRequest request)
    {
        var gender = Trh2010.NormalizeCinsiyet(request.Cinsiyet);
        var disabilityValue = request.MaluliyetOrani;
        if (gender is null || disabilityValue <= 0 || disabilityValue > 100)
        {
            throw new BadRequestException("cinsiyet (erkek|kadin), yas ve 0-100 arası maluliyet_orani zorunludur.");
        }

        var faultRate = request.KusurOrani ?? 0m;
        if (faultRate is < 0 or > 100)
        {
            throw new BadRequestException("kusur_orani 0-100 arasında olmalıdır.");
        }

        var temporaryDisabilityMonths = request.GeciciIsGoremezlikAy ?? 0m;
        if (temporaryDisabilityMonths < 0)
        {
            throw new BadRequestException("gecici_is_goremezlik_ay negatif olamaz.");
        }

        var monthlyIncome = request.AylikNetGelir ?? MinimumWage.GuncelAylikNet;
        if (monthlyIncome <= 0)
        {
            throw new BadRequestException("aylik_net_gelir pozitif olmalıdır.");
        }

        if (ActuarialMethod.Hesapla(gender, request.Yas) is not { } period)
        {
            throw new BadRequestException("yas 0-100 aralığında olmalıdır.");
        }

        var disabilityFraction = disabilityValue / 100m;
        var faultFraction = faultRate / 100m;
        var annualIncome = monthlyIncome * 12m;

        var activeLoss = decimal.Round(annualIncome * period.AktifYil * disabilityFraction, 2);
        var passiveLoss = decimal.Round(MinimumWage.GuncelYillikNet * period.PasifYil * disabilityFraction, 2);
        var temporaryLoss = decimal.Round(monthlyIncome * temporaryDisabilityMonths, 2);
        var grossTotal = activeLoss + passiveLoss + temporaryLoss;
        var faultDeduction = decimal.Round(grossTotal * faultFraction, 2);
        var netCompensation = grossTotal - faultDeduction;

        var assumptions = new List<string>
        {
            "Bakiye ömür TRH-2010 tablosundan, cinsiyet ve kaza yaşına göre alındı.",
            "Mahkeme modu progresif rant: %10 artırım + %10 iskonto birbirini götürür, net teknik faiz %0; toplam kayıp = yıllık kayıp x yıl.",
            $"Aktif dönem kaza yaşından {ActuarialMethod.AktifDonemSonuYas} yaşına kadardır; {ActuarialMethod.AktifDonemSonuYas} ve üzeri yaşta aktif dönem 0 alınır (çalışmanın sürdüğü belgelenirse hesap değişir).",
            $"Pasif dönem geliri AGİ hariç net asgari ücrettir ({MinimumWage.GuncelAylikNet:N2} TL/ay).",
            "Geçici iş göremezlik döneminde %100 iş göremezlik ve mağdurun kendi geliri esas alındı.",
            "Kusur indirimi mağdurun kusur oranıdır; karşı tarafın kusuru girilmemelidir.",
        };
        if (request.AylikNetGelir is null)
        {
            assumptions.Add("aylik_net_gelir verilmediğinden aktif dönem için de AGİ hariç net asgari ücret esas alındı.");
        }

        return new BodilyInjuryCompensationResponse(
            "Aktüeryal bedensel tazminat, mahkeme modu (TRH-2010 + progresif rant %0 net teknik faiz)",
            decimal.Round(period.BakiyeOmur, 2), decimal.Round(period.AktifYil, 2), decimal.Round(period.PasifYil, 2),
            decimal.Round(monthlyIncome, 2), activeLoss, passiveLoss, temporaryLoss, grossTotal, faultRate, faultDeduction,
            netCompensation, $"ZMSS (trafik sigortası) 2026 kişi başı sakatlık teminat limiti {Zmss.KisiBasiTeminatLimiti2026:N2} TL'dir; sigortacıdan talep bu limitle sınırlıdır, aşan kısım işletene/sürücüye yöneltilir.",
            assumptions, $"{Trh2010.Kaynak}; {MinimumWage.Kaynak}; {Zmss.TeminatKaynak}");
    }

    // ---- calc_harc ----

    public CourtFeeResponse CalcCourtFee(CourtFeeRequest request)
    {
        var operation = request.Islem;
        var caseType = request.DavaTuru ?? "genel";
        if (caseType is not ("genel" or "olum_cismani"))
        {
            throw new BadRequestException("dava_turu genel veya olum_cismani olmalıdır.");
        }

        var advanceRate = caseType == "olum_cismani" ? CourtFees2026.OlumCismaniPesinOran : CourtFees2026.PesinOran;
        var value = request.DavaDegeri;
        var items = new List<CourtFeeItem>();
        var notes = new List<string>();

        void Add(string name, decimal amount) => items.Add(new CourtFeeItem(name, decimal.Round(amount, 2)));

        switch (operation)
        {
            case "dava_acilis":
            {
                var filingFee = request.Mahkeme switch
                {
                    "sulh" => CourtFees2026.BasvurmaSulh,
                    _ => CourtFees2026.BasvurmaAsliyeIdare,
                };
                Add("Başvurma harcı", filingFee);
                if (request.Maktu)
                {
                    Add("Maktu karar ve ilam harcı (peşin)", CourtFees2026.MaktuKararIlam);
                }
                else
                {
                    if (value is not { } amount || amount <= 0)
                    {
                        throw new BadRequestException("dava_acilis nispi hesabı için pozitif dava_degeri gerekir (maktu dava için maktu=true gönderin).");
                    }

                    var decisionFee = amount * CourtFees2026.KararIlamNispiOran;
                    var advance = decimal.Round(decisionFee * advanceRate, 2);
                    Add(caseType == "olum_cismani"
                        ? "Nispi karar ve ilam harcı peşini (1/20, ölüm/cismani zarar)"
                        : "Nispi karar ve ilam harcı peşini (1/4)", advance);
                    notes.Add($"Nispi karar ve ilam harcının tamamı (binde 68,31) {decimal.Round(decisionFee, 2)} TL'dir; kalan kısım karar kesinleşmeden önce tamamlatılır.");
                }

                break;
            }

            case "islah_deger_artirimi":
            {
                var oldValue = request.EskiDeger;
                var newValue = request.YeniDeger;
                if (oldValue is not { } o || newValue is not { } n || o < 0 || n <= o)
                {
                    throw new BadRequestException("islah_deger_artirimi için eski_deger ve ondan büyük yeni_deger zorunludur.");
                }

                var additionalFee = decimal.Round((n - o) * CourtFees2026.KararIlamNispiOran * advanceRate, 2);
                Add("Islah/değer artırımı ek peşin harcı", additionalFee);
                notes.Add($"Ek harç = (yeni değer - eski değer) x binde 68,31 x peşin oran; artırılan tutar {decimal.Round(n - o, 2)} TL.");
                break;
            }

            case "istinaf":
                Add("İstinaf kanun yoluna başvurma harcı", CourtFees2026.IstinafBasvurma);
                Add("İstinaf karar harcı (maktu)", CourtFees2026.MaktuKararIlam);
                break;

            case "temyiz":
                Add("Temyiz kanun yoluna başvurma harcı", CourtFees2026.TemyizBasvurma);
                Add("Temyiz karar harcı (maktu)", CourtFees2026.MaktuKararIlam);
                break;

            case "icra_takip":
            {
                if (value is not { } amount || amount <= 0)
                {
                    throw new BadRequestException("icra_takip için pozitif dava_degeri (takip tutarı) gerekir.");
                }

                Add("İcra başvurma harcı", CourtFees2026.BasvurmaIcra);
                Add("İcra peşin harcı (binde 5)", decimal.Round(amount * CourtFees2026.IcraPesinOran, 2));
                notes.Add("Tahsil harcı tahsilat aşamasına göre ayrıca doğar: hacizden önce %4,55, hacizden sonra %9,10, satıştan sonra %11,38.");
                break;
            }

            default:
                throw new BadRequestException("islem dava_acilis, islah_deger_artirimi, istinaf, temyiz veya icra_takip olmalıdır.");
        }

        var total = items.Sum(i => i.Tutar);
        return new CourtFeeResponse(operation, caseType, items, decimal.Round(total, 2), notes, CourtFees2026.Donem, CourtFees2026.Kaynak);
    }

    // ---- calc_sure ----

    public DeadlineResponse CalcDeadline(DeadlineRequest request)
    {
        const string source = "HMK m.92-93, m.104, m.127, m.345, m.361; CMK m.273, m.291; İİK m.62, m.67; İYUK m.8, m.45, m.46, m.61; " + CourtCalendar.Kaynak;

        if (request.TebligTarihi is not { } serviceDate)
        {
            throw new BadRequestException("sure_turu ve teblig_tarihi zorunludur.");
        }

        var definition = request.SureTuru switch
        {
            "istinaf_hukuk" => new DurationDefinition(0, 2, 0, "HMK m.345: istinaf süresi 2 hafta", true, false),
            "istinaf_ceza" => new DurationDefinition(7, 0, 0, "CMK m.273: istinaf süresi 7 gün", false, false),
            "temyiz_hukuk" => new DurationDefinition(0, 2, 0, "HMK m.361: temyiz süresi 2 hafta", true, false),
            "temyiz_ceza" => new DurationDefinition(15, 0, 0, "CMK m.291: temyiz süresi 15 gün", false, false),
            "cevap_dilekcesi" => new DurationDefinition(0, 2, 0, "HMK m.127: cevap süresi 2 hafta", true, false),
            "icra_itiraz" => new DurationDefinition(7, 0, 0, "İİK m.62: ödeme emrine itiraz 7 gün", false, false),
            "itirazin_iptali" => new DurationDefinition(0, 0, 1, "İİK m.67: itirazın iptali davası 1 yıl", false, false),
            "istinaf_idare" => new DurationDefinition(30, 0, 0, "İYUK m.45: istinaf süresi 30 gün", false, true),
            "temyiz_idare" => new DurationDefinition(30, 0, 0, "İYUK m.46: temyiz süresi 30 gün", false, true),
            _ => null,
        };
        if (definition is null)
        {
            throw new BadRequestException("Geçersiz sure_turu.");
        }

        var rules = new List<string>
        {
            "Süre tebliğ tarihinin ertesi günü işlemeye başlar (HMK m.92 / CMK m.39 / İİK m.19 / İYUK m.8).",
            definition.Basis,
        };

        var lastDay = serviceDate.AddYears(definition.Years).AddDays((definition.Weeks * 7) + definition.Days);
        rules.Add($"Ham son gün: {lastDay:dd.MM.yyyy}.");

        if (definition.ExtendsInJudicialRecess && CourtCalendar.IsAdliTatil(lastDay))
        {
            lastDay = CourtCalendar.AdliTatilUzamasi(lastDay.Year);
            rules.Add($"Son gün adli tatile (20 Temmuz - 31 Ağustos) rastladı; süre tatilin bittiği günden itibaren 1 hafta uzadı (HMK m.104): {lastDay:dd.MM.yyyy}.");
        }

        if (definition.AdministrativeRecess && CourtCalendar.IsAdliTatil(lastDay))
        {
            lastDay = CourtCalendar.AdliTatilUzamasi(lastDay.Year);
            rules.Add($"Son gün çalışmaya ara verme dönemine (20 Temmuz - 31 Ağustos) rastladı; süre ara vermenin bittiği günü izleyen günden itibaren 7 gün uzadı (İYUK m.8/3, m.61): {lastDay:dd.MM.yyyy}.");
        }

        var warnings = new List<string>();
        while (true)
        {
            if (CourtCalendar.IsWeekend(lastDay))
            {
                rules.Add($"{lastDay:dd.MM.yyyy} hafta sonuna rastladığından süre ilk iş gününe uzadı (HMK m.93).");
                lastDay = lastDay.AddDays(1);
                continue;
            }

            if (CourtCalendar.HolidayName(lastDay) is { } holidayName)
            {
                rules.Add($"{lastDay:dd.MM.yyyy} resmi tatile ({holidayName}) rastladığından süre ilk iş gününe uzadı (HMK m.93).");
                lastDay = lastDay.AddDays(1);
                continue;
            }

            break;
        }

        if (!CourtCalendar.HasReligiousHolidayData(lastDay.Year))
        {
            warnings.Add($"{lastDay.Year} yılının dini bayram günleri tabloda yok; son gün bir dini bayrama denk geliyorsa süre ayrıca uzar, kontrol edin.");
        }

        warnings.Add(
            "Arife günleri ve 28 Ekim yarım gün tatildir ve burada ihtiyatlı yorumla süreyi uzatır kabul edildi; uygulamada yarım günde mesai öğlene kadar sürer. Ceza sürelerinde tefhimle başlama, idari yargıda ara kararlar ve özel kanunlardaki farklı süreler için dosya özelinde kontrol gerekir; süreler hak düşürücüdür.");

        return new DeadlineResponse(request.SureTuru, serviceDate, lastDay, rules, warnings, source);
    }

    private sealed record DurationDefinition(int Days, int Weeks, int Years, string Basis, bool ExtendsInJudicialRecess, bool AdministrativeRecess);

    // ---- get_exchange_rates ----

    public async Task<ExchangeRatesResponse> GetExchangeRatesAsync(ExchangeRatesRequest request, CancellationToken ct = default)
    {
        var codes = request.ParaBirimleri is { Count: > 0 } requested
            ? requested.Select(c => c.Trim().ToUpperInvariant()).Where(s => s.Length > 0).Distinct().ToArray()
            : ["USD", "EUR", "GBP"];
        if (codes.Length == 0)
        {
            throw new BadRequestException("para_birimleri boş olamaz; en az bir ISO kodu verin (örn. USD).");
        }

        TcmbBulletin? bulletin;
        try
        {
            bulletin = request.Tarih is { } d ? await tcmb.GetForDateAsync(d, ct) : await tcmb.GetLatestAsync(ct);
        }
        catch (HttpRequestException)
        {
            throw new BadRequestException("TCMB kur servisine ulaşılamadı; lütfen daha sonra tekrar deneyin.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new BadRequestException("TCMB kur servisi zaman aşımına uğradı; lütfen daha sonra tekrar deneyin.");
        }

        if (bulletin is null)
        {
            throw new BadRequestException(
                "İstenen tarih için TCMB kur bülteni bulunamadı (7 güne kadar önceki iş günlerine de bakıldı); tarihi kontrol edin.");
        }

        var rateList = new List<ExchangeRateItem>(codes.Length);
        var missingCodes = new List<string>();
        foreach (var code in codes)
        {
            var rate = bulletin.Rates.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));
            if (rate is null)
            {
                missingCodes.Add(code);
                continue;
            }

            rateList.Add(new ExchangeRateItem(rate.Code, rate.Unit, rate.ForexBuying, rate.ForexSelling));
        }

        if (rateList.Count == 0)
        {
            throw new BadRequestException($"İstenen para birimleri TCMB bülteninde bulunamadı: {string.Join(", ", missingCodes)}.");
        }

        return new ExchangeRatesResponse(bulletin.Date, rateList, missingCodes, "TCMB Gösterge Kurları");
    }

    // ---- calc_smm ----

    public FreelanceReceiptResponse CalcFreelanceReceipt(FreelanceReceiptRequest request)
    {
        const string source =
            "GVK m.94/2-b (serbest meslek stopajı %20); KDV genel oranı %20 (7346 CB Kararı); avukatlık hizmetlerinde 5/10 KDV tevkifatı: KDV Genel Uygulama Tebliği I/C-2.1.3.2.13";

        var direction = request.Yon;
        var value = request.Tutar;
        if (value <= 0)
        {
            throw new BadRequestException("yon ve pozitif tutar zorunludur.");
        }

        var vatRate = request.KdvOrani ?? 20m;
        var withholdingRate = request.StopajOrani ?? 20m;
        if (vatRate is < 0 or > 100 || withholdingRate is < 0 or >= 100)
        {
            throw new BadRequestException("kdv_orani 0-100, stopaj_orani 0-99 aralığında olmalıdır.");
        }

        var vatFraction = vatRate / 100m;
        var withholdingFraction = withholdingRate / 100m;
        decimal gross = direction switch
        {
            "brutten" => value,
            "netten" => value / (1m - withholdingFraction),
            "kdv_dahilden" => value / (1m + vatFraction),
            _ => throw new BadRequestException("yon brutten, netten veya kdv_dahilden olmalıdır."),
        };

        var reverseCharge = request.KdvTevkifati;
        var withholdingAmount = gross * withholdingFraction;
        var vatAmount = gross * vatFraction;
        var vatReverseChargeAmount = reverseCharge ? vatAmount * 0.5m : 0m;
        var net = gross - withholdingAmount;
        var totalCollected = net + vatAmount - vatReverseChargeAmount;

        return new FreelanceReceiptResponse(
            decimal.Round(gross, 2), withholdingRate, decimal.Round(withholdingAmount, 2), decimal.Round(net, 2),
            vatRate, decimal.Round(vatAmount, 2), decimal.Round(vatReverseChargeAmount, 2), decimal.Round(totalCollected, 2),
            "Stopaj yalnızca GVK m.94 kapsamındaki vergi sorumlusu ödeyicilerde (şirket, kurum vb.) kesilir; nihai tüketiciye makbuzda stopaj uygulanmaz. 5/10 KDV tevkifatı yalnızca tevkifat yükümlüsü alıcılarda geçerlidir.",
            source);
    }

    // ---- calc_brut_net_maas ----

    public GrossNetSalaryResponse CalcGrossNetSalary(GrossNetSalaryRequest request)
    {
        string[] assumptions =
        [
            "2026 parametreleri: brüt asgari ücret 33.030,00 TL, SGK işçi %14 + işsizlik %1, SGK tavanı 247.725,00 TL/ay, damga binde 7,59",
            "Gelir vergisi 2026 ücret tarifesi; kümülatif matrah, tüm aylarda aynı brüt ücret varsayımıyla ay konumuna göre hesaplanır",
            "Asgari ücret istisnası uygulanır: ücretin asgari ücrete isabet eden kısmının gelir ve damga vergisi alınmaz",
            "Tek işveren, 4/a sigortalısı; engellilik indirimi, BES kesintisi, özel sigorta ve teşvikler hesaba katılmamıştır",
        ];

        var direction = request.Yon;
        var amount = request.Tutar;
        if (direction is not ("brutten_nete" or "netten_brute") || amount <= 0)
        {
            throw new BadRequestException("yon (brutten_nete | netten_brute) ve pozitif tutar zorunludur.");
        }

        var month = request.Ay ?? 1;
        if (month is < 1 or > 12)
        {
            throw new BadRequestException("ay 1 ile 12 arasında olmalıdır.");
        }

        var grossAmount = direction == "brutten_nete" ? amount : NetToGross(amount, month);
        var payroll = Payroll(grossAmount, month);

        decimal annualNet = 0m;
        for (var m = 1; m <= 12; m++)
        {
            annualNet += Payroll(grossAmount, m).Net;
        }

        return new GrossNetSalaryResponse(
            direction, month, decimal.Round(payroll.Gross, 2), decimal.Round(payroll.Net, 2),
            new GrossNetSalaryDeductions(
                payroll.SocialSecurity, payroll.Unemployment, payroll.IncomeTax, payroll.StampTax,
                payroll.SocialSecurity + payroll.Unemployment + payroll.IncomeTax + payroll.StampTax),
            new GrossNetSalaryExemption(payroll.IncomeTaxExemption, payroll.StampTaxExemption),
            new GrossNetSalaryProjection(
                decimal.Round(grossAmount * 12m, 2), decimal.Round(annualNet, 2),
                "Tüm yıl aynı brüt ücretle çalışıldığı varsayılarak 12 ayın kümülatif vergili net toplamı"),
            assumptions, "2026", PayrollParameters2026.Kaynak);
    }

    private sealed record MonthlyPayroll(
        decimal Gross, decimal SocialSecurity, decimal Unemployment, decimal IncomeTax, decimal StampTax,
        decimal IncomeTaxExemption, decimal StampTaxExemption, decimal Net);

    private static MonthlyPayroll Payroll(decimal grossAmount, int month)
    {
        var socialSecurityBase = Math.Min(grossAmount, PayrollParameters2026.SgkTavanAylik);
        var socialSecurity = decimal.Round(socialSecurityBase * PayrollParameters2026.SgkIsciOrani, 2);
        var unemployment = decimal.Round(socialSecurityBase * PayrollParameters2026.IssizlikIsciOrani, 2);
        var taxBase = grossAmount - socialSecurity - unemployment;

        var incomeTaxFull = PayrollParameters2026.TarifeVergisi(taxBase * month) - PayrollParameters2026.TarifeVergisi(taxBase * (month - 1));

        var exemptionBase = Math.Min(taxBase, PayrollParameters2026.AsgariUcretBrut * 0.85m);
        var exemption = PayrollParameters2026.TarifeVergisi(exemptionBase * month) - PayrollParameters2026.TarifeVergisi(exemptionBase * (month - 1));
        var incomeTax = decimal.Round(Math.Max(0m, incomeTaxFull - exemption), 2);

        var stampExemptionBase = Math.Min(grossAmount, PayrollParameters2026.AsgariUcretBrut);
        var stampTax = decimal.Round(Math.Max(0m, (grossAmount - stampExemptionBase) * PayrollParameters2026.DamgaOrani), 2);

        var net = grossAmount - socialSecurity - unemployment - incomeTax - stampTax;
        return new MonthlyPayroll(
            grossAmount, socialSecurity, unemployment, incomeTax, stampTax,
            decimal.Round(Math.Min(incomeTaxFull, exemption), 2),
            decimal.Round(stampExemptionBase * PayrollParameters2026.DamgaOrani, 2), net);
    }

    private static decimal NetToGross(decimal targetNet, int month)
    {
        var lo = targetNet;
        var hi = targetNet * 2m;
        for (var i = 0; i < 80; i++)
        {
            var mid = (lo + hi) / 2m;
            if (Payroll(mid, month).Net < targetNet)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return decimal.Round(hi, 2);
    }

    // ---- calc_miras_pay ----

    public InheritanceShareResponse CalcInheritanceShare(InheritanceShareRequest request)
    {
        const string source =
            "TMK m.495-501 (zümre sistemi ve yasal mirasçılar), m.499 (sağ kalan eşin payı), m.505-506 (saklı pay oranları), m.501 (mirasçı yoksa Devlet)";

        if (request.EsVar is not { } hasSpouse || request.CocukSayisi < 0)
        {
            throw new BadRequestException("es_var ve negatif olmayan cocuk_sayisi zorunludur.");
        }

        var childCount = request.CocukSayisi;
        var hasMother = request.AnneVar;
        var hasFather = request.BabaVar;
        var siblingCount = request.KardesSayisi ?? 0;
        if (siblingCount < 0)
        {
            throw new BadRequestException("kardes_sayisi negatif olamaz.");
        }

        var estateValue = request.MirasDegeri;
        var warnings = new List<string>();
        var heirs = new List<Heir>();

        if (childCount > 0)
        {
            var descendantsTotal = Fraction.One;
            if (hasSpouse)
            {
                var spouseShare = Fraction.Olustur(1, 4);
                heirs.Add(new Heir("es", 1, spouseShare, spouseShare));
                descendantsTotal = Fraction.Olustur(3, 4);
            }

            heirs.Add(new Heir("cocuklar", childCount, descendantsTotal, Fraction.Carp(descendantsTotal, Fraction.Olustur(1, 2))));
            warnings.Add("Mirasbırakandan önce ölen çocuğun yerini kendi altsoyu (torunlar) alır; bu halefiyet burada modellenmedi.");
        }
        else if (hasMother || hasFather || siblingCount > 0)
        {
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
                heirs.Add(new Heir("es", 1, spouseShare, spouseShare));
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
            heirs.Add(new Heir("es", 1, Fraction.One, Fraction.Olustur(3, 4)));
            warnings.Add("Büyük ana-baba veya onların altsoyu (3. zümre) sağ ise eşin yasal payı 3/4'e iner (TMK m.499/3); bu girdilerle 3. zümre sorgulanamadığından eş tek mirasçı varsayıldı. Tek başına yasal mirasçı olan eşin saklı payı yasal payının 3/4'üdür (TMK m.506/4).");
        }
        else
        {
            return new InheritanceShareResponse(
                [], null, null, null, null, null,
                "Girilen bilgilere göre yasal mirasçı yok; mirasçı bırakmadan ölen kimsenin mirası Devlete geçer (TMK m.501).", null, source);
        }

        var reservedTotal = Fraction.Zero;
        var shareTotal = Fraction.Zero;
        var output = new List<InheritanceHeir>(heirs.Count);
        foreach (var heir in heirs)
        {
            shareTotal = Fraction.Topla(shareTotal, heir.TotalShare);
            reservedTotal = Fraction.Topla(reservedTotal, heir.ReservedShare);
            var perPerson = heir.PersonCount > 1 ? Fraction.Bol(heir.TotalShare, heir.PersonCount) : heir.TotalShare;
            output.Add(new InheritanceHeir(
                heir.Role, heir.PersonCount, heir.TotalShare.Text, perPerson.Text, heir.TotalShare.Percentage,
                heir.TotalShare.Tutar(estateValue), perPerson.Tutar(estateValue), heir.ReservedShare.Text,
                heir.ReservedShare.Tutar(estateValue)));
        }

        var disposable = Fraction.Cikar(Fraction.One, reservedTotal);
        warnings.Add(
            "Hesap sadeleştirilmiş bir zümre modelidir: evlatlık, mal rejimi tasfiyesi (eşin katılma alacağı terekeden önce ayrılır), mirastan feragat/yoksunluk ve vasiyetnameler dikkate alınmadı; kesin paylaşım için tereke dosyası incelenmelidir.");

        return new InheritanceShareResponse(
            output, shareTotal.Text, reservedTotal.Text, disposable.Text, disposable.Percentage,
            disposable.Tutar(estateValue), null, warnings, source);
    }

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

    // ---- calc_veraset_intikal ----

    public InheritanceTaxResponse CalcInheritanceTax(InheritanceTaxRequest request)
    {
        var transferType = request.IntikalTuru;
        var value = request.Matrah;
        if (transferType is not ("veraset" or "ivazsiz") || value <= 0)
        {
            throw new BadRequestException("intikal_turu (veraset | ivazsiz) ve pozitif matrah zorunludur.");
        }

        var exemption = transferType == "veraset"
            ? (request.EsTekBasina ? InheritanceTax2026.IstisnaEsTekBasina : InheritanceTax2026.IstisnaFuruVeEs)
            : InheritanceTax2026.IstisnaIvazsiz;
        var taxableAmount = value > exemption ? value - exemption : 0m;

        var table = transferType == "veraset" ? InheritanceTax2026.Veraset : InheritanceTax2026.Ivazsiz;
        var slices = BracketSchedule.Apply(table, taxableAmount);
        var tax = decimal.Round(BracketSchedule.Total(slices), 2);
        var breakdown = slices.Select(s => new InheritanceTaxBracketItem(s.DilimTutari, s.Yuzde, decimal.Round(s.Pay, 2))).ToList();

        return new InheritanceTaxResponse(
            transferType, value, exemption, taxableAmount, breakdown, tax, decimal.Round(tax / value * 100m, 2),
            "Veraset istisnası mirasçı başına hisse üzerinden uygulanır. Ana, baba, eş ve çocuklardan ivazsız intikalde tarife oranlarının yarısı uygulanır (7338 sK m.16), bu araç bu indirimi otomatik uygulamaz.",
            InheritanceTax2026.Donem, InheritanceTax2026.Kaynak);
    }

    // ---- calc_faiz ----

    public InterestResponse CalcInterest(InterestRequest request)
    {
        var interestType = request.FaizTuru;
        var principal = request.AnaPara;
        if (principal <= 0 || request.Baslangic is not { } start || request.Bitis is not { } end)
        {
            throw new BadRequestException("faiz_turu, pozitif ana_para, baslangic ve bitis zorunludur.");
        }

        var endExclusive = request.BitisDahil ? end.AddDays(1) : end;
        if (start >= endExclusive)
        {
            throw new BadRequestException("baslangic, bitis tarihinden önce olmalıdır.");
        }

        RatePeriod[] table;
        switch (interestType)
        {
            case "yasal":
                table = LegalInterestRates.Yasal;
                break;
            case "avans":
                table = LegalInterestRates.Avans;
                break;
            case "ozel":
                if (request.OzelOran is not { } customRate || customRate <= 0)
                {
                    throw new BadRequestException("faiz_turu=ozel için pozitif ozel_oran gerekir.");
                }

                table = [new RatePeriod(DateOnly.MinValue, null, customRate)];
                break;
            default:
                throw new BadRequestException("faiz_turu yasal, avans veya ozel olmalıdır.");
        }

        if (!RateSchedule.Covers(table, start))
        {
            throw new BadRequestException(
                $"{interestType} faiz tablosu {table[0].From:dd.MM.yyyy} öncesini kapsamıyor; daha eski dönem için ozel_oran ile hesaplayın.");
        }

        var segments = RateSchedule.Segments(table, start, endExclusive);
        decimal totalInterest = 0;
        var breakdown = new List<InterestSegment>(segments.Count);
        foreach (var s in segments)
        {
            var interest = principal * (s.AnnualPercent / 100m) * s.Days / 365m;
            totalInterest += interest;
            breakdown.Add(new InterestSegment(s.From, s.To, s.AnnualPercent, s.Days, decimal.Round(interest, 2)));
        }

        totalInterest = decimal.Round(totalInterest, 2);
        return new InterestResponse(
            "Basit faiz, ACT/365, dönem tablosuna göre parçalı", endExclusive.DayNumber - start.DayNumber, breakdown,
            totalInterest, decimal.Round(principal + totalInterest, 2), LegalInterestRates.Kaynak);
    }

    // ---- calc_adli_para_cezasi ----

    public JudicialFineResponse CalcJudicialFine(JudicialFineRequest request)
    {
        const decimal dailyAmountMin = 100m;
        const decimal dailyAmountMax = 500m;
        const int dayCountMin = 5;
        const int dayCountMax = 730;
        const string source =
            "TCK m.52 (günlük miktar 100-500 TL: 7499 sK m.9, RG 12.03.2024/32487; 12.03.2024 öncesi işlenen suçlarda 20-100 TL); 5275 sK m.106 (ödenmeyen adli para cezasının hapse çevrilmesi)";

        var days = request.GunSayisi;
        var amount = request.GunlukMiktar;
        if (days <= 0 || amount <= 0)
        {
            throw new BadRequestException("Pozitif gun_sayisi ve gunluk_miktar zorunludur.");
        }

        var warnings = new List<string>();
        if (days is < dayCountMin or > dayCountMax)
        {
            warnings.Add($"Gün sayısı TCK m.52/1'deki {dayCountMin}-{dayCountMax} gün aralığının dışında; kanunda daha yüksek gün öngören özel hükümler saklıdır.");
        }

        if (amount is < dailyAmountMin or > dailyAmountMax)
        {
            warnings.Add($"Günlük miktar güncel {dailyAmountMin:0}-{dailyAmountMax:0} TL aralığının dışında (7499 sK, 12.03.2024 sonrası suçlar); 12.03.2024 öncesi işlenen suçlarda 20-100 TL aralığı uygulanır.");
        }

        var total = decimal.Round(days * amount, 2);
        string? installmentRule = null;
        if (request.Taksit)
        {
            installmentRule =
                $"TCK m.52/4: taksitle ödemeye karar verilebilir; taksit süresi 2 yılı geçemez ve taksit sayısı 4'ten az olamaz. Örnek 4 eşit taksit: {decimal.Round(total / 4, 2)} TL. Taksitlerden biri zamanında ödenmezse kalan kısmın tamamı tahsil edilir, ödenmeyen kısım hapse çevrilir.";
        }

        return new JudicialFineResponse(
            days, amount, total, days,
            "Adli para cezası ödenmezse gün sayısı kadar hapse çevrilir (5275 m.106); kamuya yararlı işte çalışma seçeneği saklıdır.",
            installmentRule, warnings, source);
    }

    // ---- calc_gecikme_zammi ----

    public LatePaymentSurchargeResponse CalcLatePaymentSurcharge(LatePaymentSurchargeRequest request)
    {
        var principalValue = request.AnaPara;
        if (principalValue <= 0 || request.VadeTarihi is not { } start || request.OdemeTarihi is not { } end)
        {
            throw new BadRequestException("Pozitif ana_para, vade_tarihi ve odeme_tarihi zorunludur.");
        }

        if (start >= end)
        {
            throw new BadRequestException("odeme_tarihi vade_tarihi'nden sonra olmalıdır; vadesinde veya öncesinde ödemede gecikme zammı doğmaz.");
        }

        var table = DelaySurchargeRates.Table;
        if (start < table[0].From)
        {
            throw new BadRequestException($"Gecikme zammı oran tablosu {table[0].From:dd.MM.yyyy} öncesini kapsamıyor; daha eski vadeler için dönem oranlarını ayrıca doğrulayın.");
        }

        decimal totalSurcharge = 0;
        var totalMonths = 0;
        var totalFractionDays = 0;
        var breakdown = new List<LatePaymentSurchargeSegment>();
        foreach (var period in table)
        {
            var from = period.From > start ? period.From : start;
            var to = period.To is { } t && t < end ? t : end;
            if (from >= to)
            {
                continue;
            }

            var anchor = from;
            var months = 0;
            while (anchor.AddMonths(1) <= to)
            {
                anchor = anchor.AddMonths(1);
                months++;
            }

            var days = to.DayNumber - anchor.DayNumber;
            var surcharge = principalValue * (period.MonthlyPercent / 100m) * (months + days / 30m);
            totalSurcharge += surcharge;
            totalMonths += months;
            totalFractionDays += days;
            breakdown.Add(new LatePaymentSurchargeSegment(from, to, period.MonthlyPercent, months, days, decimal.Round(surcharge, 2)));
        }

        totalSurcharge = decimal.Round(totalSurcharge, 2);
        string? note = null;
        if (totalSurcharge < 1m)
        {
            totalSurcharge = 1.00m;
            note = "Hesaplanan zam 1 TL'nin altında kaldığından asgari 1 TL uygulandı (6183 m.51/2).";
        }

        return new LatePaymentSurchargeResponse(
            "6183 m.51: süre vade tarihinden ödeme tarihine kadardır; tam aylar aylık oranla, ay kesirleri günlük (aylık oran / 30) hesaplanır, oran dönemleri ayrı ayrı uygulanır.",
            totalMonths, totalFractionDays, breakdown, totalSurcharge, decimal.Round(principalValue + totalSurcharge, 2), note,
            DelaySurchargeRates.Kaynak);
    }

    // ---- calc_destekten_yoksunluk ----

    public LossOfSupportResponse CalcLossOfSupport(LossOfSupportRequest request)
    {
        var gender = Trh2010.NormalizeCinsiyet(request.Cinsiyet);
        if (gender is null)
        {
            throw new BadRequestException("cinsiyet (erkek|kadin) ve yas zorunludur.");
        }

        var ageValue = request.Yas;
        var faultRate = request.KusurOrani ?? 0m;
        if (faultRate is < 0 or > 100)
        {
            throw new BadRequestException("kusur_orani 0-100 arasında olmalıdır.");
        }

        var monthlyIncome = request.AylikNetGelir ?? MinimumWage.GuncelAylikNet;
        if (monthlyIncome <= 0)
        {
            throw new BadRequestException("aylik_net_gelir pozitif olmalıdır.");
        }

        if (ActuarialMethod.Hesapla(gender, ageValue) is not { } period)
        {
            throw new BadRequestException("yas 0-100 aralığında olmalıdır.");
        }

        var childAges = request.Cocuklar ?? [];
        foreach (var childAge in childAges)
        {
            if (childAge is < 0 or > 100)
            {
                throw new BadRequestException("cocuklar tam sayı yaş listesi olmalıdır (0-100).");
            }
        }

        var hasSpouse = request.EsVar;
        var hasMother = request.AnneVar;
        var hasFather = request.BabaVar;
        if (!hasSpouse && !hasMother && !hasFather && childAges.Count == 0)
        {
            throw new BadRequestException("En az bir hak sahibi (es_var, cocuklar, anne_var, baba_var) belirtilmelidir.");
        }

        var assumptions = new List<string>
        {
            "Bakiye ömürler TRH-2010 tablosundan alındı; destek süresi her hak sahibi için müteveffanın bakiye ömrüyle sınırlıdır.",
            "Mahkeme modu progresif rant: %10 artırım + %10 iskonto birbirini götürür, net teknik faiz %0; destek = yıllık destek x yıl.",
            $"Aktif dönem müteveffanın {ActuarialMethod.AktifDonemSonuYas} yaşına kadardır; pasif dönemde AGİ hariç net asgari ücret ({MinimumWage.GuncelAylikNet:N2} TL/ay) esas alınır.",
            "Pay dağılımı yerleşik uygulama: müteveffa 2, eş 2, her çocuk 1, anne ve baba 1'er pay; müteveffanın payı kendi tüketimi sayılır ve dağıtılmaz.",
            "Çocuklara destek 18 yaşına kadar hesaplanır (kız/erkek ayrımı ve eğitim uzatması bu sürümde yok).",
            "Eşin yeniden evlenme ihtimali indirimi bu sürümde uygulanmamaktadır; bilirkişi hesabında AYİM/TRH pratiğine göre indirim yapılabilir.",
        };

        var beneficiaries = new List<(string Name, int Age, string Gender, int Share, decimal? SupportYears)>();
        if (hasSpouse)
        {
            var spouseAge = request.EsYasi ?? ageValue;
            if (request.EsYasi is null)
            {
                assumptions.Add("es_yasi verilmediğinden eş müteveffayla aynı yaşta varsayıldı.");
            }

            var spouseGender = gender == "erkek" ? "kadin" : "erkek";
            assumptions.Add("Eşin cinsiyeti müteveffanın karşı cinsi varsayıldı (bakiye ömür tablosu seçimi için).");
            beneficiaries.Add(("es", spouseAge, spouseGender, 2, null));
        }

        for (var i = 0; i < childAges.Count; i++)
        {
            beneficiaries.Add(($"cocuk_{i + 1}", childAges[i], gender, 1, Math.Max(0m, 18 - childAges[i])));
        }

        if (hasMother)
        {
            var motherAge = request.AnneYasi ?? Math.Min(ageValue + 25, 100);
            if (request.AnneYasi is null)
            {
                assumptions.Add("anne_yasi verilmediğinden müteveffanın yaşı + 25 varsayıldı.");
            }

            beneficiaries.Add(("anne", motherAge, "kadin", 1, null));
        }

        if (hasFather)
        {
            var fatherAge = request.BabaYasi ?? Math.Min(ageValue + 25, 100);
            if (request.BabaYasi is null)
            {
                assumptions.Add("baba_yasi verilmediğinden müteveffanın yaşı + 25 varsayıldı.");
            }

            beneficiaries.Add(("baba", fatherAge, "erkek", 1, null));
        }

        var totalShares = 2 + beneficiaries.Sum(b => b.Share);
        var faultFraction = faultRate / 100m;
        var annualIncome = monthlyIncome * 12m;
        var annualMinimumWage = MinimumWage.GuncelYillikNet;

        decimal grossTotal = 0, netTotal = 0;
        var breakdown = new List<LossOfSupportBeneficiary>(beneficiaries.Count);
        foreach (var (name, beneficiaryAge, beneficiaryGender, share, fixedSupportYears) in beneficiaries)
        {
            decimal supportYears;
            if (fixedSupportYears is { } childFixedYears)
            {
                supportYears = Math.Min(childFixedYears, period.BakiyeOmur);
            }
            else if (Trh2010.BakiyeOmur(beneficiaryGender, beneficiaryAge) is { } ownResidualLife)
            {
                supportYears = Math.Min(ownResidualLife, period.BakiyeOmur);
            }
            else
            {
                throw new BadRequestException($"{name} için yaş 0-100 aralığında olmalıdır.");
            }

            var activePart = Math.Min(supportYears, period.AktifYil);
            var passivePart = Math.Max(0m, supportYears - activePart);
            var shareFraction = (decimal)share / totalShares;
            var gross = decimal.Round((annualIncome * activePart + annualMinimumWage * passivePart) * shareFraction, 2);
            var net = decimal.Round(gross * (1m - faultFraction), 2);
            grossTotal += gross;
            netTotal += net;
            breakdown.Add(new LossOfSupportBeneficiary(
                name, beneficiaryAge, share, decimal.Round(supportYears, 2), decimal.Round(activePart, 2),
                decimal.Round(passivePart, 2), gross, net));
        }

        return new LossOfSupportResponse(
            "Destekten yoksun kalma tazminatı, mahkeme modu (TRH-2010 + progresif rant %0 net teknik faiz, klasik pay havuzu)",
            decimal.Round(period.BakiyeOmur, 2), decimal.Round(period.AktifYil, 2), decimal.Round(period.PasifYil, 2),
            decimal.Round(monthlyIncome, 2), totalShares, faultRate, breakdown, grossTotal, netTotal, assumptions,
            $"{Trh2010.Kaynak}; {MinimumWage.Kaynak}");
    }

    // ---- calc_arabuluculuk_ucreti ----

    public MediationFeeResponse CalcMediationFee(MediationFeeRequest request)
    {
        const string mandatoryMediationNote =
            "Dava şartı arabuluculukta taraflar anlaşamazsa ilk 2 saatlik ücret Adalet Bakanlığı bütçesinden ödenir; aşan kısım aksi kararlaştırılmadıkça taraflarca eşit karşılanır.";

        var disputeType = request.UyusmazlikTuru;
        if (!MediationFees2026.Saatlik.TryGetValue(disputeType, out var hourlyTable))
        {
            throw new BadRequestException("uyusmazlik_turu zorunludur; geçerli değerler: " + string.Join(", ", MediationFees2026.Saatlik.Keys) + ".");
        }

        var partyCount = request.TarafSayisi ?? 2;
        if (partyCount < 2)
        {
            throw new BadRequestException("taraf_sayisi en az 2 olmalıdır.");
        }

        if (!request.Anlasma)
        {
            if (request.Saat is not { } hours || hours < 1)
            {
                throw new BadRequestException("Anlaşma yoksa pozitif saat zorunludur.");
            }

            var hourlyRate = hourlyTable[MediationFees2026.TarafBandi(partyCount)];
            return new MediationFeeResponse(
                disputeType, "saatlik", null, partyCount, decimal.Round(hourlyRate, 2), hours, null, null, null,
                null, null, null, decimal.Round(hourlyRate * hours, 2), mandatoryMediationNote, MediationFees2026.Donem, MediationFees2026.Kaynak);
        }

        if (request.AnlasmaTutari is not { } amount || amount <= 0)
        {
            throw new BadRequestException("Anlaşma varsa pozitif anlasma_tutari zorunludur.");
        }

        var multipleMediators = (request.ArabulucuSayisi ?? 1) > 1;
        var table = multipleMediators ? MediationFees2026.AnlasmaCokluArabulucu : MediationFees2026.AnlasmaTekArabulucu;
        var slices = BracketSchedule.Apply(table, amount);
        var proportionalFee = decimal.Round(BracketSchedule.Total(slices), 2);

        var mandatoryMediation = request.DavaSarti != false;
        var minimumFee = mandatoryMediation
            ? disputeType is "ticari" or "ortakligin_giderilmesi" ? MediationFees2026.AsgariAnlasmaTicariOrtaklik : MediationFees2026.AsgariAnlasmaGenel
            : 0m;
        var minimumApplied = proportionalFee < minimumFee;
        var breakdown = slices.Select(s => new MediationFeeBracketItem(s.DilimTutari, s.Yuzde, decimal.Round(s.Pay, 2))).ToList();

        return new MediationFeeResponse(
            disputeType, "anlasma_nispi", mandatoryMediation, null, null, null, amount,
            multipleMediators ? "birden_fazla" : "tek", breakdown, proportionalFee, minimumFee, minimumApplied,
            minimumApplied ? minimumFee : proportionalFee,
            mandatoryMediation ? mandatoryMediationNote : "İhtiyari arabuluculukta anlaşma ücretine asgari ücret tabanı uygulanmaz; ücret tamamen nispi dilim tablosundan hesaplanır.",
            MediationFees2026.Donem, MediationFees2026.Kaynak);
    }

    // ---- calc_ihbar_tazminati ----

    public NoticePayResponse CalcNoticePay(NoticePayRequest request)
    {
        var baseSalary = request.BrutMaas;
        if (baseSalary <= 0)
        {
            throw new BadRequestException("Pozitif brut_maas zorunludur.");
        }

        decimal seniorityMonths;
        if (request.KidemAy is { } months)
        {
            if (months < 0)
            {
                throw new BadRequestException("kidem_ay negatif olamaz.");
            }

            seniorityMonths = months;
        }
        else if (request.IseGiris is { } hireDate && request.IstenCikis is { } terminationDate)
        {
            if (hireDate >= terminationDate)
            {
                throw new BadRequestException("ise_giris, isten_cikis tarihinden önce olmalıdır.");
            }

            seniorityMonths = (terminationDate.DayNumber - hireDate.DayNumber) / 30m;
        }
        else
        {
            throw new BadRequestException("kidem_ay veya ise_giris + isten_cikis tarih çifti zorunludur.");
        }

        var mealAllowance = request.Yemek ?? 0m;
        var transportAllowance = request.Yol ?? 0m;
        var otherAllowances = request.Diger ?? 0m;
        if (mealAllowance < 0 || transportAllowance < 0 || otherAllowances < 0)
        {
            throw new BadRequestException("yemek, yol ve diger negatif olamaz.");
        }

        var weeks = seniorityMonths switch
        {
            < 6m => 2,
            < 18m => 4,
            <= 36m => 6,
            _ => 8,
        };
        var grossedUp = baseSalary + mealAllowance + transportAllowance + otherAllowances;
        var dailyWage = grossedUp / 30m;
        var grossNoticePay = dailyWage * 7m * weeks;

        var incomeTax = decimal.Round(PayrollParameters2026.TarifeVergisi(grossNoticePay), 2);
        var stampTax = decimal.Round(grossNoticePay * PayrollParameters2026.DamgaOrani, 2);
        var grossNoticePayRounded = decimal.Round(grossNoticePay, 2);

        return new NoticePayResponse(
            decimal.Round(seniorityMonths, 2), weeks, decimal.Round(grossedUp, 2), decimal.Round(dailyWage, 2),
            grossNoticePayRounded, incomeTax, stampTax, grossNoticePayRounded - incomeTax - stampTax,
            "İhbar tazminatı gelir vergisine ve binde 7,59 damga vergisine tabidir, SGK primi kesilmez. Gelir vergisi burada tazminat tutarının 2026 ücret tarifesine tek başına uygulanmasıyla yaklaşık hesaplanmıştır; gerçek kesinti işçinin yıl içi kümülatif matrahındaki dilime göre değişir.",
            "4857 sK m.17; GVK m.61 ve m.103 (2026 tarifesi); 488 sK IV/1-b (binde 7,59)");
    }

    // ---- calc_fazla_mesai ----

    public OvertimePayResponse CalcOvertimePay(OvertimePayRequest request)
    {
        var monthlyGross = request.AylikBrutUcret;
        if (monthlyGross <= 0)
        {
            throw new BadRequestException("Pozitif aylik_brut_ucret zorunludur.");
        }

        var overtimeHours = request.FazlaMesaiSaat ?? 0m;
        var weeklyRestHours = request.HaftaTatiliSaat ?? 0m;
        var holidayDays = request.ResmiTatilGun ?? 0m;
        if (overtimeHours < 0 || weeklyRestHours < 0 || holidayDays < 0)
        {
            throw new BadRequestException("Saat ve gün değerleri negatif olamaz.");
        }

        var hourlyWage = monthlyGross / 225m;
        var dailyWage = monthlyGross / 30m;
        var overtimePay = overtimeHours * hourlyWage * 1.5m;
        var weeklyRestPay = weeklyRestHours * hourlyWage * 2.5m;
        var holidayPay = holidayDays * dailyWage * 2m;

        var overtimePayRounded = decimal.Round(overtimePay, 2);
        var weeklyRestPayRounded = decimal.Round(weeklyRestPay, 2);
        var holidayPayRounded = decimal.Round(holidayPay, 2);

        return new OvertimePayResponse(
            decimal.Round(hourlyWage, 2), decimal.Round(dailyWage, 2), overtimePayRounded, weeklyRestPayRounded,
            holidayPayRounded, overtimePayRounded + weeklyRestPayRounded + holidayPayRounded,
            "Tutarlar brüttür; ücret gibi gelir vergisi, damga vergisi ve SGK primine tabidir. Hafta tatili ve UBGT hesaplarında maktu aylık içinde ödenmiş 1 yevmiyelik kısım toplam hak edilen tutara dahildir; bakiye alacak hesabında ödenen kısım mahsup edilebilir.",
            "4857 sK m.41 (fazla çalışma %50 zamlı), m.46 (hafta tatili) ve m.47 (UBGT); hafta tatili x2,5 uygulaması Yargıtay 9. HD yerleşik içtihadı");
    }

    // ---- calc_ise_iade_tazminati ----

    public ReinstatementPayResponse CalcReinstatementPay(ReinstatementPayRequest request)
    {
        var monthlyGross = request.AylikBrut;
        var seniorityYears = request.KidemYil;
        if (monthlyGross <= 0 || seniorityYears < 0)
        {
            throw new BadRequestException("Pozitif aylik_brut ve negatif olmayan kidem_yil zorunludur.");
        }

        const int idlePeriodMonths = 4;
        var idlePeriodPay = decimal.Round(monthlyGross * idlePeriodMonths, 2);

        var (minMonths, maxMonths) = seniorityYears switch
        {
            < 0.5m => (4, 4),
            < 5m => (4, 5),
            < 15m => (5, 6),
            _ => (6, 8),
        };

        return new ReinstatementPayResponse(
            idlePeriodMonths, idlePeriodPay, $"{minMonths}-{maxMonths} ay", decimal.Round(monthlyGross * minMonths, 2),
            decimal.Round(monthlyGross * maxMonths, 2), decimal.Round(monthlyGross * (idlePeriodMonths + minMonths), 2),
            decimal.Round(monthlyGross * (idlePeriodMonths + maxMonths), 2),
            "İşe başlatmama tazminatının ay sayısı 4-8 aralığında mahkeme takdirindedir; verilen bant yerleşik uygulamadan çıkarılan pratik bir tahmindir. Boşta geçen süre ücreti giydirilmiş ücretten, işe başlatmama tazminatı çıplak brüt ücretten hesaplanır. İş güvencesi için en az 30 işçili işyeri, 6 ay kıdem ve belirsiz süreli sözleşme şartları aranır (4857 sK m.18).",
            "4857 sK m.18 (iş güvencesi kapsamı) ve m.21 (işe başlatmama tazminatı 4-8 ay, boşta geçen süre en çok 4 ay)");
    }

    // ---- calc_kira_artis ----

    public RentIncreaseResponse CalcRentIncrease(RentIncreaseRequest request)
    {
        var currentRent = request.MevcutKira;
        if (currentRent <= 0)
        {
            throw new BadRequestException("Pozitif mevcut_kira ve yenileme_ayi (YYYY-MM) zorunludur.");
        }

        if (!DateOnly.TryParseExact(
                request.YenilemeAyi + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var renewalDate))
        {
            throw new BadRequestException("yenileme_ayi YYYY-MM biçiminde olmalıdır (örn. 2026-07).");
        }

        var indexDate = renewalDate.AddMonths(-1);
        var customRate = request.TufeOrani;
        if (customRate is { } customRateValue && customRateValue < 0)
        {
            throw new BadRequestException("tufe_orani negatif olamaz.");
        }

        var rate = customRate ?? TufeTwelveMonthAverage.For(indexDate.Year, indexDate.Month);
        if (rate is not { } appliedRate)
        {
            throw new BadRequestException(
                $"{indexDate.Year:0000}-{indexDate.Month:00} endeks ayı için 12 aylık ortalama TÜFE gömülü tabloda yok; TÜİK'in o ay için açıkladığı oranı tufe_orani parametresiyle verin.");
        }

        var increase = decimal.Round(currentRent * appliedRate / 100m, 2);
        var source = customRate is null
            ? $"{TufeTwelveMonthAverage.Kaynak} (endeks ayı {indexDate.Year:0000}-{indexDate.Month:00})"
            : "Kullanıcının verdiği TÜFE oranı; TBK m.344";

        return new RentIncreaseResponse(
            decimal.Round(currentRent, 2), appliedRate, $"{indexDate.Year:0000}-{indexDate.Month:00}", increase,
            decimal.Round(currentRent + increase, 2),
            "TBK m.344: artış üst sınırı bir önceki kira yılına ait 12 aylık ortalama TÜFE'dir; çatılı işyeri kiralarında da TÜFE sınırı uygulanır. Taraflar daha düşük bir oran kararlaştırabilir.",
            source);
    }

    // ---- calc_infaz ----

    public SentenceExecutionResponse CalcSentenceExecution(SentenceExecutionRequest request)
    {
        string[] offenseTypes = ["adi", "kasten_oldurme", "cinsel", "uyusturucu_ticareti", "teror_orgutlu"];
        string[] specialCircumstances = ["yok", "cocuklu_kadin", "agir_hasta_yasli"];

        if (request.SucTarihi is not { } offense || request.InfazBaslangic is not { } start)
        {
            throw new BadRequestException("suc_tarihi, ceza_turu, suc_tipi ve infaz_baslangic zorunludur.");
        }

        var sentenceType = request.CezaTuru;
        var offenseType = request.SucTipi;
        if (Array.IndexOf(offenseTypes, offenseType) < 0)
        {
            throw new BadRequestException("suc_tipi adi, kasten_oldurme, cinsel, uyusturucu_ticareti veya teror_orgutlu olmalıdır.");
        }

        var recidivism = request.Tekerrur ?? 0;
        if (recidivism is < 0 or > 2)
        {
            throw new BadRequestException("tekerrur 0, 1 veya 2 olmalıdır.");
        }

        var offsetDays = request.MahsupGun ?? 0;
        if (offsetDays < 0)
        {
            throw new BadRequestException("mahsup_gun negatif olamaz.");
        }

        var specialCircumstance = request.OzelDurum ?? "yok";
        if (Array.IndexOf(specialCircumstances, specialCircumstance) < 0)
        {
            throw new BadRequestException("ozel_durum yok, cocuklu_kadin veya agir_hasta_yasli olmalıdır.");
        }

        var warnings = new List<string>();
        DateOnly conditionalReleaseDate;
        DateOnly? unconditionalReleaseDate;
        int? totalSentenceDays = null;
        string ratioText;

        switch (sentenceType)
        {
            case "sureli":
            {
                var years = request.CezaYil ?? 0;
                var months = request.CezaAy ?? 0;
                var days = request.CezaGun ?? 0;
                if (years < 0 || months < 0 || days < 0 || years + months + days == 0)
                {
                    throw new BadRequestException("Süreli cezada ceza_yil/ceza_ay/ceza_gun ile pozitif bir toplam ceza girilmelidir.");
                }

                var rawUnconditionalReleaseDate = start.AddYears(years).AddMonths(months).AddDays(days);
                totalSentenceDays = rawUnconditionalReleaseDate.DayNumber - start.DayNumber;
                var (numerator, denominator) = SentenceExecutionRates.KosulluSaliverilmeOrani(offenseType, recidivism);
                var conditionalReleaseDays = (int)Math.Ceiling(totalSentenceDays.Value * (decimal)numerator / denominator);
                conditionalReleaseDate = start.AddDays(conditionalReleaseDays - offsetDays);
                unconditionalReleaseDate = rawUnconditionalReleaseDate.AddDays(-offsetDays);
                ratioText = $"{numerator}/{denominator}";
                break;
            }

            case "muebbet":
            case "agirlastirilmis_muebbet":
            {
                var conditionalReleaseYears = SentenceExecutionRates.MuebbetKosulluYil(sentenceType, recidivism);
                conditionalReleaseDate = start.AddYears(conditionalReleaseYears).AddDays(-offsetDays);
                unconditionalReleaseDate = null;
                ratioText = $"{conditionalReleaseYears} yıl";
                warnings.Add("Müebbet cezalarda bihakkın tahliye yoktur; infaz koşullu salıverilme ve denetim süresiyle sona erer.");
                break;
            }

            default:
                throw new BadRequestException("ceza_turu sureli, muebbet veya agirlastirilmis_muebbet olmalıdır.");
        }

        var probationYears = SentenceExecutionRates.DenetimliSerbestlikYil(specialCircumstance);
        if (SentenceExecutionRates.GeciciM10DenetimliSerbestlikYil(offense, offenseType) is { } transitionalYears)
        {
            if (transitionalYears > probationYears)
            {
                probationYears = transitionalYears;
            }

            warnings.Add(
                "7571 sK ile değişik 5275 geçici m.10 uygulandı (suç tarihi 31.07.2023 ve öncesi): burada basitleştirilmiş olarak 30.03.2020 öncesi suçlarda 3 yıl, 30.03.2020 - 31.07.2023 arası suçlarda 2 yıl denetimli serbestlik esas alındı. Gerçek düzenleme ayrıca açık ceza infaz kurumuna 3 yıl erken ayrılma ve kademeli ek süreler içerir; kesin tarih infaz savcılığının müddetnamesine göre belirlenir. Geçici maddedeki istisna suçlar (kasten öldürme, cinsel dokunulmazlığa karşı suçlar vb.) için ayrıca kontrol gerekir.");
        }

        var probationStartDate = conditionalReleaseDate.AddYears(-probationYears);
        if (probationStartDate < start)
        {
            probationStartDate = start;
            warnings.Add("Hesaplanan denetimli serbestlik tarihi infaz başlangıcından önce; hükümlü koşulları sağlıyorsa doğrudan denetimli serbestlik değerlendirilebilir.");
        }

        if (request.DogumTarihi is { } birthDate && offense < birthDate.AddYears(18))
        {
            warnings.Add("Suç tarihinde hükümlü 18 yaşından küçük: çocuklara özgü infaz rejimi (5275 m.107/5 artırımlı gün sayma, eğitimevi) uygulanmalıdır; bu hesap yetişkin rejimiyle yapılmıştır.");
        }

        warnings.Add(
            "Bu hesap iyi hal değerlendirmesi, disiplin cezaları, cezaların içtimaı, hücre cezası süreleri ve infaz hakimliği kararlarını dikkate almaz; bağlayıcı hesap infaz savcılığının düzenlediği müddetnamedir.");

        return new SentenceExecutionResponse(
            sentenceType, offenseType, totalSentenceDays, offsetDays, ratioText, conditionalReleaseDate, probationYears,
            probationStartDate, unconditionalReleaseDate, probationStartDate.DayNumber - start.DayNumber, warnings,
            SentenceExecutionRates.Kaynak);
    }

    // ---- calc_kidem_tazminati ----

    public SeveranceResponse CalcSeverance(SeveranceRequest request)
    {
        var baseSalary = request.BrutMaas;
        if (baseSalary <= 0 || request.IseGiris is not { } start || request.IstenCikis is not { } end)
        {
            throw new BadRequestException("ise_giris, isten_cikis ve pozitif brut_maas zorunludur.");
        }

        if (start >= end)
        {
            throw new BadRequestException("ise_giris, isten_cikis tarihinden önce olmalıdır.");
        }

        var mealAllowance = request.Yemek ?? 0m;
        var transportAllowance = request.Yol ?? 0m;
        var otherAllowances = request.Diger ?? 0m;
        if (mealAllowance < 0 || transportAllowance < 0 || otherAllowances < 0)
        {
            throw new BadRequestException("yemek, yol ve diger negatif olamaz.");
        }

        var totalDays = end.DayNumber - start.DayNumber;
        if (totalDays < 365)
        {
            throw new BadRequestException(
                "Kıdem 1 yıldan az: 1475 sK m.14 uyarınca kıdem tazminatı hakkı, en az 1 tam yıl çalışmayla doğar. Bu kıdemle kıdem tazminatı 0 TL'dir.");
        }

        if (SeveranceCeiling.At(end) is not { } ceilingPeriod)
        {
            throw new BadRequestException(
                $"Kıdem tavanı tablosu {SeveranceCeiling.Periods[0].From:dd.MM.yyyy} öncesini kapsamıyor; bu fesih tarihi için dönem tavanını elle doğrulayın.");
        }

        var grossedUp = baseSalary + mealAllowance + transportAllowance + otherAllowances;
        var ceilingApplied = grossedUp > ceilingPeriod.Amount;
        var baseWage = ceilingApplied ? ceilingPeriod.Amount : grossedUp;

        var fullYears = totalDays / 365;
        var remainingDays = totalDays % 365;
        var grossSeverance = (baseWage * fullYears) + (baseWage * remainingDays / 365m);
        var stampTax = decimal.Round(grossSeverance * PayrollParameters2026.DamgaOrani, 2);
        var grossSeveranceRounded = decimal.Round(grossSeverance, 2);

        var tavanDonemi = ceilingPeriod.To is { } to
            ? $"{ceilingPeriod.From:dd.MM.yyyy} - {to.AddDays(-1):dd.MM.yyyy}"
            : $"{ceilingPeriod.From:dd.MM.yyyy} - devam ediyor";

        return new SeveranceResponse(
            totalDays, fullYears, remainingDays, decimal.Round(grossedUp, 2), ceilingPeriod.Amount, tavanDonemi,
            ceilingApplied, decimal.Round(baseWage, 2), grossSeveranceRounded, stampTax, grossSeveranceRounded - stampTax,
            "Kıdem tazminatından yalnızca binde 7,59 damga vergisi kesilir; gelir vergisi ve SGK primi kesilmez. Hesap, son giydirilmiş brüt ücretin fesih tarihindeki tavanla sınırlanmasıyla yapılır.",
            SeveranceCeiling.Kaynak);
    }

    // ---- calc_damga_vergisi ----

    public StampDutyResponse CalcStampDuty(StampDutyRequest request)
    {
        var documentType = request.KagitTuru;
        if (!StampDuty2026.Binde.TryGetValue(documentType, out var perMilleRate))
        {
            throw new BadRequestException("kagit_turu zorunludur; geçerli değerler: " + string.Join(", ", StampDuty2026.Binde.Keys) + ".");
        }

        var value = request.Tutar;
        if (value <= 0)
        {
            throw new BadRequestException("Pozitif tutar zorunludur.");
        }

        var calculated = decimal.Round(value * perMilleRate / 1000m, 2);
        var capApplied = calculated > StampDuty2026.AzamiVergi;

        return new StampDutyResponse(
            documentType, value, perMilleRate, calculated, StampDuty2026.AzamiVergi, capApplied,
            capApplied ? StampDuty2026.AzamiVergi : calculated, StampDuty2026.Donem, StampDuty2026.Kaynak);
    }

    // ---- calc_zamanasimi ----

    public StatuteOfLimitationsResponse CalcStatuteOfLimitations(StatuteOfLimitationsRequest request)
    {
        const string source =
            "TBK m.146 (genel 10 yıl), m.147 (5 yıllık süreler), m.72 (haksız fiil 2/10 yıl); TCK m.66 (dava zamanaşımı), m.67 (durma-kesilme), m.68 (ceza zamanaşımı)";

        if (request.OlayTarihi is not { } eventDate)
        {
            throw new BadRequestException("alan ve olay_tarihi zorunludur.");
        }

        var warnings = new List<string>();
        int periodYears;
        string basis;
        int? extendedPeriodYears = null;

        switch (request.Alan)
        {
            case "tbk_genel":
                periodYears = 10;
                basis = "TBK m.146: kanunda aksine hüküm yoksa her alacak 10 yıllık zamanaşımına tabidir.";
                break;
            case "haksiz_fiil":
                periodYears = 2;
                extendedPeriodYears = 10;
                basis = "TBK m.72: zarar görenin zararı ve tazminat yükümlüsünü öğrenmesinden itibaren 2 yıl, her halde fiilden itibaren 10 yıl.";
                warnings.Add("2 yıllık süre öğrenme tarihinden işler; burada öğrenme tarihi olay tarihi varsayıldı. Fiil aynı zamanda suç oluşturuyorsa daha uzun olan ceza dava zamanaşımı uygulanır (TBK m.72/1).");
                break;
            case "kira":
                periodYears = 5;
                basis = "TBK m.147/1: kira bedelleri, anapara faizleri ve ücret gibi dönemsel edimler 5 yıllık zamanaşımına tabidir.";
                break;
            case "ticari_satis":
                periodYears = 5;
                basis = "TBK m.147/3: perakende satışlardan doğan alacaklar 5 yıllık zamanaşımına tabidir.";
                warnings.Add("Tacirler arası/toptan satış sözleşmesinden doğan alacaklarda genel 10 yıllık süre (TBK m.146), ayıptan sorumlulukta ise 2 yıllık süre (TBK m.231) uygulanabilir; sözleşmenin niteliği belirleyicidir.");
                break;
            case "ceza_dava":
            case "ceza_infaz":
            {
                var sentenceType = request.CezaTuru ?? "sureli";
                var upperLimitYears = request.CezaUstSiniriYil;
                if (sentenceType == "sureli" && upperLimitYears is not > 0)
                {
                    throw new BadRequestException("Süreli hapiste ceza_ust_siniri_yil (pozitif yıl) zorunludur.");
                }

                if (request.Alan == "ceza_dava")
                {
                    (periodYears, basis) = sentenceType switch
                    {
                        "agirlastirilmis_muebbet" => (30, "TCK m.66/1-a: ağırlaştırılmış müebbet gerektiren suçlarda dava zamanaşımı 30 yıl."),
                        "muebbet" => (25, "TCK m.66/1-b: müebbet hapis gerektiren suçlarda dava zamanaşımı 25 yıl."),
                        _ => upperLimitYears >= 20
                            ? (20, "TCK m.66/1-c: 20 yıldan aşağı olmayan hapis gerektiren suçlarda dava zamanaşımı 20 yıl.")
                            : upperLimitYears > 5
                                ? (15, "TCK m.66/1-d: 5 yıldan fazla, 20 yıldan az hapis gerektiren suçlarda dava zamanaşımı 15 yıl.")
                                : (8, "TCK m.66/1-e: 5 yıldan fazla olmayan hapis veya adli para cezası gerektiren suçlarda dava zamanaşımı 8 yıl."),
                    };
                    warnings.Add("Süre, suçun kanundaki cezasının üst sınırına göre belirlenir (TCK m.66/3, ağırlaştırıcı nitelikli haller dahil). Fiili işlediğinde 12-15 yaş grubundakiler için sürelerin yarısı, 15-18 yaş için üçte ikisi uygulanır (m.66/2). Kesilme halinde süre en fazla yarısına kadar uzar (m.67/4).");
                }
                else
                {
                    (periodYears, basis) = sentenceType switch
                    {
                        "agirlastirilmis_muebbet" => (40, "TCK m.68/1-a: ağırlaştırılmış müebbet hapiste ceza zamanaşımı 40 yıl."),
                        "muebbet" => (30, "TCK m.68/1-b: müebbet hapiste ceza zamanaşımı 30 yıl."),
                        _ => upperLimitYears >= 20
                            ? (24, "TCK m.68/1-c: 20 yıl ve daha fazla süreli hapiste ceza zamanaşımı 24 yıl.")
                            : upperLimitYears > 5
                                ? (20, "TCK m.68/1-d: 5 yıldan fazla hapiste ceza zamanaşımı 20 yıl.")
                                : (10, "TCK m.68/1-e: 5 yıla kadar hapis ve adli para cezalarında ceza zamanaşımı 10 yıl."),
                    };
                    warnings.Add("Süre hükmün kesinleştiği veya infazın kesintiye uğradığı günden işler (TCK m.68/5); olay_tarihi olarak kesinleşme tarihi girilmelidir. 12-18 yaş indirimleri m.68/2'de saklıdır.");
                }

                break;
            }

            default:
                throw new BadRequestException("Geçersiz alan.");
        }

        warnings.Add(
            "Zamanaşımını kesen ve durduran sebepler (TBK m.152-154: dava, icra takibi, borç ikrarı; TCK m.67: iddianame, sorgu, mahkumiyet vb.) hesaba katılmadı; kesin değerlendirme dosya üzerinden yapılmalıdır. Bu bir tahmindir, hukuki dayanak değildir.");

        return new StatuteOfLimitationsResponse(
            request.Alan, periodYears, eventDate.AddYears(periodYears), extendedPeriodYears,
            extendedPeriodYears is { } u ? eventDate.AddYears(u) : null, basis, warnings, source);
    }

    // ---- calc_tapu_harci ----

    public TitleDeedFeeResponse CalcTitleDeedFee(TitleDeedFeeRequest request)
    {
        var price = request.SatisBedeli;
        if (price <= 0)
        {
            throw new BadRequestException("Pozitif satis_bedeli zorunludur.");
        }

        var party = request.Taraf ?? "toplam";
        if (party is not ("alici" or "satici" or "toplam"))
        {
            throw new BadRequestException("taraf alici, satici veya toplam olmalıdır.");
        }

        var buyerFee = decimal.Round(price * TitleDeedFees2026.AliciOran, 2);
        var sellerFee = decimal.Round(price * TitleDeedFees2026.SaticiOran, 2);
        var fee = party switch
        {
            "alici" => buyerFee,
            "satici" => sellerFee,
            _ => buyerFee + sellerFee,
        };

        return new TitleDeedFeeResponse(
            price, party, buyerFee, sellerFee, fee,
            "Harç, beyan edilen satış bedeli ile emlak vergisi değerinin yükseği üzerinden alınır. " + TitleDeedFees2026.DonerSermayeNotu,
            TitleDeedFees2026.Donem, TitleDeedFees2026.Kaynak);
    }

    // ---- calc_arac_deger_kaybi ----

    public VehicleValueLossResponse CalcVehicleValueLoss(VehicleValueLossRequest request)
    {
        var value = request.PiyasaDegeri;
        var mileageValue = request.Kilometre;
        if (value <= 0 || mileageValue < 0)
        {
            throw new BadRequestException("Pozitif piyasa_degeri ve negatif olmayan kilometre zorunludur.");
        }

        var assumptions = new List<string>
        {
            "Resmi formül: Toplam Değer Kaybı = Baz Değer Kaybı (rayiç x %19) x hasar boyutu katsayısı x kilometre katsayısı (ZMSS Genel Şartları eki).",
        };

        var damageSize = request.HasarBoyutu;
        if (damageSize is null)
        {
            if (request.HasarTutari is not { } repairAmount || repairAmount <= 0)
            {
                throw new BadRequestException("hasar_boyutu (buyuk|orta|kucuk|basit) veya pozitif hasar_tutari verilmelidir.");
            }

            var ratio = repairAmount / value;
            damageSize = ratio switch
            {
                <= 0.05m => "basit",
                <= 0.15m => "kucuk",
                <= 0.25m => "orta",
                _ => "buyuk",
            };
            assumptions.Add(
                "Hasar boyutu onarım tutarının rayice oranından türetildi (pratik yaklaşım, Genel Şartlar'da yer almaz): %5'e kadar basit, %15'e kadar küçük, %25'e kadar orta, üzeri büyük.");
        }

        if (Zmss.HasarBoyutuKatsayisi(damageSize) is not { } damageCoefficient)
        {
            throw new BadRequestException("hasar_boyutu buyuk, orta, kucuk veya basit olmalıdır.");
        }

        var mileageCoefficient = Zmss.KilometreKatsayisi(mileageValue) ?? 0m;
        var baseAmount = decimal.Round(value * Zmss.BazDegerKaybiOrani, 2);
        var valueLoss = decimal.Round(value * Zmss.BazDegerKaybiOrani * damageCoefficient * mileageCoefficient, 2);

        return new VehicleValueLossResponse(
            "ZMSS Genel Şartları eki resmi değer kaybı formülü", baseAmount, damageSize, damageCoefficient, mileageCoefficient,
            valueLoss,
            "Bu tutar sigorta tahkim/başvuru aşaması için Genel Şartlar formülüdür; mahkemede bilirkişi serbest piyasa (rayiç fark) yöntemiyle farklı ve çoğu kez daha yüksek bir tutara ulaşabilir.",
            assumptions, Zmss.DegerKaybiKaynak);
    }

    // ---- calc_is_kazasi_tazminati ----

    public WorkAccidentCompensationResponse CalcWorkAccidentCompensation(WorkAccidentCompensationRequest request)
    {
        var gender = Trh2010.NormalizeCinsiyet(request.Cinsiyet);
        var disabilityValue = request.SurekliIsGoremezlikOrani;
        if (gender is null || disabilityValue <= 0 || disabilityValue > 100)
        {
            throw new BadRequestException("cinsiyet (erkek|kadin), yas ve 0-100 arası surekli_is_goremezlik_orani zorunludur.");
        }

        var employerFaultRate = request.IsverenKusurOrani ?? 100m;
        if (employerFaultRate is <= 0 or > 100)
        {
            throw new BadRequestException("isveren_kusur_orani 0'dan büyük ve en çok 100 olmalıdır.");
        }

        var temporaryDisabilityMonths = request.GeciciIsGoremezlikAy ?? 0m;
        if (temporaryDisabilityMonths < 0)
        {
            throw new BadRequestException("gecici_is_goremezlik_ay negatif olamaz.");
        }

        var monthlyIncome = request.AylikNetGelir ?? MinimumWage.GuncelAylikNet;
        if (monthlyIncome <= 0)
        {
            throw new BadRequestException("aylik_net_gelir pozitif olmalıdır.");
        }

        var capitalizedValue = request.SgkPesinSermaye;
        if (capitalizedValue is { } capitalizedCheck && capitalizedCheck < 0)
        {
            throw new BadRequestException("sgk_pesin_sermaye negatif olamaz.");
        }

        if (ActuarialMethod.Hesapla(gender, request.Yas) is not { } period)
        {
            throw new BadRequestException("yas 0-100 aralığında olmalıdır.");
        }

        var disabilityFraction = disabilityValue / 100m;
        var annualIncome = monthlyIncome * 12m;

        var activeLoss = decimal.Round(annualIncome * period.AktifYil * disabilityFraction, 2);
        var passiveLoss = decimal.Round(MinimumWage.GuncelYillikNet * period.PasifYil * disabilityFraction, 2);
        var temporaryLoss = decimal.Round(monthlyIncome * temporaryDisabilityMonths, 2);
        var grossTotal = activeLoss + passiveLoss + temporaryLoss;
        var compensationAfterFault = decimal.Round(grossTotal * (employerFaultRate / 100m), 2);
        var socialSecurityDeduction = capitalizedValue is { } capitalAmount ? decimal.Round(Math.Min(capitalAmount, compensationAfterFault), 2) : 0m;
        var netCompensation = compensationAfterFault - socialSecurityDeduction;

        var assumptions = new List<string>
        {
            "Bakiye ömür TRH-2010 tablosundan, cinsiyet ve kaza yaşına göre alındı.",
            "Mahkeme modu progresif rant: %10 artırım + %10 iskonto birbirini götürür, net teknik faiz %0; toplam kayıp = yıllık kayıp x yıl.",
            $"Aktif dönem kaza yaşından {ActuarialMethod.AktifDonemSonuYas} yaşına kadardır; {ActuarialMethod.AktifDonemSonuYas} ve üzeri yaşta aktif dönem 0 alınır (çalışmanın sürdüğü belgelenirse hesap değişir).",
            $"Pasif dönem geliri AGİ hariç net asgari ücrettir ({MinimumWage.GuncelAylikNet:N2} TL/ay).",
            "Geçici iş göremezlik döneminde %100 iş göremezlik ve işçinin kendi ücreti esas alındı.",
            "Tazminat işverenin kusur oranıyla çarpılır; işçinin müterafik kusuru ve kaçınılmazlık indirimi bu orana yansıtılmalıdır.",
        };
        if (request.SgkGelirBaglandi && capitalizedValue is null)
        {
            assumptions.Add(
                "SGK sürekli iş göremezlik geliri bağlandığı bildirildi ancak peşin sermaye değeri verilmedi: SGK'nın bildireceği ilk peşin sermaye değeri tazminattan ayrıca düşülmelidir; buradaki sonuç indirimsizdir.");
        }

        return new WorkAccidentCompensationResponse(
            "İş kazası maddi tazminatı, mahkeme modu (TRH-2010 + progresif rant %0 net teknik faiz)",
            decimal.Round(period.BakiyeOmur, 2), decimal.Round(period.AktifYil, 2), decimal.Round(period.PasifYil, 2),
            decimal.Round(monthlyIncome, 2), activeLoss, passiveLoss, temporaryLoss, grossTotal, employerFaultRate,
            compensationAfterFault, socialSecurityDeduction, netCompensation,
            "Bilirkişi hesabı ücret artışları, belgeli çalışma süresi ve SGK peşin sermaye güncellemeleriyle bu sonuçtan sapabilir; sonuç yaklaşık değerdir.",
            assumptions, $"{Trh2010.Kaynak}; {MinimumWage.Kaynak}");
    }
}

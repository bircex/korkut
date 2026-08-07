namespace Korkut.Contract.Calculators;

// ---- calc_nafaka ----
public sealed record AlimonyRequest(string NafakaTuru, decimal YukumluNetGelir, int? CocukSayisi, bool EsCalisiyor);
public sealed record AlimonyResponse(
    string NafakaTuru, decimal YukumluNetGelir, string OranBandi, decimal TahminiAltSinirTl,
    decimal TahminiUstSinirTl, IReadOnlyList<string> Uyarilar, string Kaynak);

// ---- calc_yillik_izin_ucreti ----
public sealed record AnnualLeavePayRequest(decimal KidemYil, decimal GunlukBrutUcret, decimal? KullanilmayanIzinGunu, int? Yas);
public sealed record AnnualLeavePayResponse(
    decimal KidemYil, int YillikIzinHakkiGun, bool YasKuraliUygulandi, decimal HesaplananGun,
    decimal GunlukBrutUcret, decimal BrutIzinUcreti, string Not, string Kaynak);

// ---- calc_vekalet_ucreti ----
public sealed record AttorneyFeeRequest(string Kategori, decimal? DavaDegeri, string? TabanKategori);
public sealed record AttorneyFeeBracketItem(decimal Dilim, decimal Yuzde, decimal Ucret);
public sealed record AttorneyFeeResponse(
    string Kategori, string UcretTuru, decimal? DavaDegeri, IReadOnlyList<AttorneyFeeBracketItem>? DilimKirilimi,
    decimal? NispiUcret, decimal? MaktuTaban, string? TabanKategori, bool? TabanUygulandi, decimal Ucret,
    string? Not, string TarifeDonemi, string Kaynak);

// ---- calc_bedensel_tazminat ----
public sealed record BodilyInjuryCompensationRequest(
    string Cinsiyet, int Yas, decimal MaluliyetOrani, decimal? KusurOrani, decimal? AylikNetGelir, decimal? GeciciIsGoremezlikAy);
public sealed record BodilyInjuryCompensationResponse(
    string Yontem, decimal BakiyeOmurYil, decimal AktifDonemYil, decimal PasifDonemYil, decimal EsasAlinanAylikNetGelir,
    decimal AktifDonemKaybi, decimal PasifDonemKaybi, decimal GeciciIsGoremezlikKaybi, decimal BrutToplam,
    decimal KusurOrani, decimal KusurIndirimi, decimal NetTazminat, string ZmssLimitNotu,
    IReadOnlyList<string> Varsayimlar, string Kaynak);

// ---- calc_harc ----
public sealed record CourtFeeRequest(
    string Islem, string? DavaTuru, decimal? DavaDegeri, decimal? EskiDeger, decimal? YeniDeger, bool Maktu,
    string? Mahkeme, string? TahsilatAsamasi, decimal? TahsilatTutari);
public sealed record CourtFeeItem(string Kalem, decimal Tutar);
public sealed record CourtFeeResponse(
    string Islem, string DavaTuru, IReadOnlyList<CourtFeeItem> Kalemler, decimal Toplam,
    IReadOnlyList<string> Notlar, string TarifeDonemi, string Kaynak);

// ---- calc_sure ----
public sealed record DeadlineRequest(string SureTuru, DateOnly? TebligTarihi);
public sealed record DeadlineResponse(
    string SureTuru, DateOnly TebligTarihi, DateOnly SonGun, IReadOnlyList<string> Kurallar,
    IReadOnlyList<string> Uyarilar, string Kaynak);

// ---- get_exchange_rates ----
public sealed record ExchangeRatesRequest(IReadOnlyList<string>? ParaBirimleri, DateOnly? Tarih);
public sealed record ExchangeRateItem(string Kod, int Birim, decimal? Alis, decimal? Satis);
public sealed record ExchangeRatesResponse(
    DateOnly KurTarihi, IReadOnlyList<ExchangeRateItem> Kurlar, IReadOnlyList<string> BulunamayanKodlar, string Kaynak);

// ---- calc_smm ----
public sealed record FreelanceReceiptRequest(string Yon, decimal Tutar, decimal? KdvOrani, decimal? StopajOrani, bool KdvTevkifati);
public sealed record FreelanceReceiptResponse(
    decimal Brut, decimal StopajOrani, decimal Stopaj, decimal NetEleGecen, decimal KdvOrani, decimal Kdv,
    decimal KdvTevkifati, decimal TahsilEdilecekToplam, string Not, string Kaynak);

// ---- calc_brut_net_maas ----
public sealed record GrossNetSalaryRequest(string Yon, decimal Tutar, int? Ay);
public sealed record GrossNetSalaryDeductions(decimal SgkIsci, decimal IssizlikIsci, decimal GelirVergisi, decimal DamgaVergisi, decimal Toplam);
public sealed record GrossNetSalaryExemption(decimal GelirVergisi, decimal DamgaVergisi);
public sealed record GrossNetSalaryProjection(decimal YillikBrut, decimal YillikNet, string Aciklama);
public sealed record GrossNetSalaryResponse(
    string Yon, int Ay, decimal BrutAylik, decimal NetAylik, GrossNetSalaryDeductions Kesintiler,
    GrossNetSalaryExemption AsgariUcretIstisnasi, GrossNetSalaryProjection YillikProjeksiyon,
    IReadOnlyList<string> Varsayimlar, string TarifeDonemi, string Kaynak);

// ---- calc_miras_pay ----
public sealed record InheritanceShareRequest(
    bool? EsVar, int CocukSayisi, bool AnneVar, bool BabaVar, int? KardesSayisi, decimal? MirasDegeri);
public sealed record InheritanceHeir(
    string Rol, int KisiSayisi, string ToplamPay, string KisiBasiPay, decimal Yuzde, decimal? ToplamTutarTl,
    decimal? KisiBasiTutarTl, string SakliPay, decimal? SakliPayTutarTl);
public sealed record InheritanceShareResponse(
    IReadOnlyList<InheritanceHeir> Mirascilar, string? PayToplami, string? SakliPayToplami,
    string? TasarrufEdilebilirKisim, decimal? TasarrufEdilebilirYuzde, decimal? TasarrufEdilebilirTutarTl,
    string? Aciklama, IReadOnlyList<string>? Uyarilar, string Kaynak);

// ---- calc_veraset_intikal ----
public sealed record InheritanceTaxRequest(string IntikalTuru, decimal Matrah, bool EsTekBasina);
public sealed record InheritanceTaxBracketItem(decimal Dilim, decimal Yuzde, decimal Vergi);
public sealed record InheritanceTaxResponse(
    string IntikalTuru, decimal Matrah, decimal Istisna, decimal VergiyeTabiMatrah,
    IReadOnlyList<InheritanceTaxBracketItem> DilimKirilimi, decimal Vergi, decimal EfektifOranYuzde,
    string Not, string TarifeDonemi, string Kaynak);

// ---- calc_faiz ----
public sealed record InterestRequest(string FaizTuru, decimal AnaPara, DateOnly? Baslangic, DateOnly? Bitis, bool BitisDahil, decimal? OzelOran);
public sealed record InterestSegment(DateOnly Baslangic, DateOnly? Bitis, decimal YillikOran, int Gun, decimal Faiz);
public sealed record InterestResponse(
    string Yontem, int ToplamGun, IReadOnlyList<InterestSegment> DonemKirilimi, decimal ToplamFaiz,
    decimal AnaParaArtiFaiz, string Kaynak);

// ---- calc_adli_para_cezasi ----
public sealed record JudicialFineRequest(int GunSayisi, decimal GunlukMiktar, bool Taksit);
public sealed record JudicialFineResponse(
    int GunSayisi, decimal GunlukMiktar, decimal ToplamCeza, int HapseCevirmeGun, string HapseCevirmeAciklama,
    string? TaksitKurali, IReadOnlyList<string> Uyarilar, string Kaynak);

// ---- calc_gecikme_zammi ----
public sealed record LatePaymentSurchargeRequest(decimal AnaPara, DateOnly? VadeTarihi, DateOnly? OdemeTarihi);
public sealed record LatePaymentSurchargeSegment(DateOnly Baslangic, DateOnly Bitis, decimal AylikOran, int TamAy, int KesirGun, decimal Zam);
public sealed record LatePaymentSurchargeResponse(
    string Yontem, int ToplamTamAy, int ToplamKesirGun, IReadOnlyList<LatePaymentSurchargeSegment> DonemKirilimi,
    decimal ToplamZam, decimal GenelToplam, string? Not, string Kaynak);

// ---- calc_destekten_yoksunluk ----
public sealed record LossOfSupportRequest(
    string Cinsiyet, int Yas, decimal? AylikNetGelir, decimal? KusurOrani, bool EsVar, int? EsYasi,
    IReadOnlyList<int>? Cocuklar, bool AnneVar, int? AnneYasi, bool BabaVar, int? BabaYasi);
public sealed record LossOfSupportBeneficiary(
    string HakSahibi, int Yas, int Pay, decimal DestekYili, decimal AktifYil, decimal PasifYil, decimal BrutDestek, decimal NetDestek);
public sealed record LossOfSupportResponse(
    string Yontem, decimal MuteveffaBakiyeOmurYil, decimal AktifDonemYil, decimal PasifDonemYil,
    decimal EsasAlinanAylikNetGelir, int ToplamPay, decimal KusurOrani, IReadOnlyList<LossOfSupportBeneficiary> HakSahipleri,
    decimal BrutToplam, decimal NetToplam, IReadOnlyList<string> Varsayimlar, string Kaynak);

// ---- calc_askerlik_borclanmasi ----
public sealed record MilitaryServiceBorrowingRequest(int GunSayisi, decimal? GunlukTutar);
public sealed record MilitaryServiceBorrowingResponse(
    int GunSayisi, decimal GunlukTutar, decimal GunlukAltSinir, decimal GunlukUstSinir, decimal PrimOrani,
    decimal BorclanmaTutari, IReadOnlyList<string> Uyarilar, string Kaynak);

// ---- calc_arabuluculuk_ucreti ----
public sealed record MediationFeeRequest(
    string UyusmazlikTuru, bool Anlasma, decimal? AnlasmaTutari, int? TarafSayisi, int? Saat, int? ArabulucuSayisi, bool? DavaSarti);
public sealed record MediationFeeBracketItem(decimal Dilim, decimal Yuzde, decimal Ucret);
public sealed record MediationFeeResponse(
    string UyusmazlikTuru, string UcretTuru, bool? DavaSarti, int? TarafSayisi, decimal? SaatlikUcret, int? Saat,
    decimal? AnlasmaTutari, string? ArabulucuSayisi, IReadOnlyList<MediationFeeBracketItem>? DilimKirilimi,
    decimal? NispiUcret, decimal? AsgariAnlasmaUcreti, bool? AsgariUygulandi, decimal Ucret, string Not,
    string TarifeDonemi, string Kaynak);

// ---- calc_ihbar_tazminati ----
public sealed record NoticePayRequest(
    decimal? KidemAy, DateOnly? IseGiris, DateOnly? IstenCikis, decimal BrutMaas, decimal? Yemek, decimal? Yol, decimal? Diger);
public sealed record NoticePayResponse(
    decimal KidemAy, int IhbarSuresiHafta, decimal GiydirilmisBrut, decimal GunlukUcret, decimal BrutIhbarTazminati,
    decimal GelirVergisiYaklasik, decimal DamgaVergisi, decimal NetIhbarTazminatiYaklasik, string Not, string Kaynak);

// ---- calc_yurtdisi_borclanma ----
public sealed record OverseasBorrowingRequest(int GunSayisi, decimal? GunlukTutar);
public sealed record OverseasBorrowingResponse(
    int GunSayisi, decimal GunlukTutar, decimal GunlukAltSinir, decimal GunlukUstSinir, decimal PrimOrani,
    decimal BorclanmaTutari, IReadOnlyList<string> Uyarilar, string Kaynak);

// ---- calc_fazla_mesai ----
public sealed record OvertimePayRequest(decimal AylikBrutUcret, decimal? FazlaMesaiSaat, decimal? HaftaTatiliSaat, decimal? ResmiTatilGun);
public sealed record OvertimePayResponse(
    decimal SaatlikUcret, decimal GunlukUcret, decimal FazlaMesaiBrut, decimal HaftaTatiliBrut, decimal ResmiTatilBrut,
    decimal ToplamBrut, string Not, string Kaynak);

// ---- calc_ise_iade_tazminati ----
public sealed record ReinstatementPayRequest(decimal AylikBrut, decimal KidemYil);
public sealed record ReinstatementPayResponse(
    int BostaGecenSureAy, decimal BostaGecenSureUcreti, string IseBaslatmamaAyAraligi, decimal IseBaslatmamaTazminatiMin,
    decimal IseBaslatmamaTazminatiMax, decimal ToplamMin, decimal ToplamMax, string Uyari, string Kaynak);

// ---- calc_emeklilik_ikramiyesi ----
public sealed record RetirementBonusRequest(decimal EkeatBrut, decimal HizmetYili);
public sealed record RetirementBonusResponse(
    decimal EkeatBrut, decimal TamHizmetYili, decimal KesirYil, decimal Ikramiye, IReadOnlyList<string> Uyarilar, string Kaynak);

// ---- calc_kira_artis ----
public sealed record RentIncreaseRequest(decimal MevcutKira, string YenilemeAyi, decimal? TufeOrani);
public sealed record RentIncreaseResponse(
    decimal MevcutKira, decimal UygulananOran, string EndeksAyi, decimal AzamiArtisTutari, decimal AzamiYeniKira,
    string Not, string Kaynak);

// ---- calc_kira_stopaji ----
public sealed record RentWithholdingRequest(string Yon, decimal Tutar, bool OdeyenStopajaTabi);
public sealed record RentWithholdingResponse(
    string Yon, decimal NetKira, decimal StopajOrani, decimal Stopaj, decimal BrutKira, bool StopajUygulandi,
    string Not, string Kaynak);

// ---- calc_infaz ----
public sealed record SentenceExecutionRequest(
    DateOnly? SucTarihi, DateOnly? DogumTarihi, string CezaTuru, int? CezaYil, int? CezaAy, int? CezaGun,
    string SucTipi, int? Tekerrur, DateOnly? InfazBaslangic, int? MahsupGun, string? OzelDurum);
public sealed record SentenceExecutionResponse(
    string CezaTuru, string SucTipi, int? ToplamCezaGun, int MahsupGun, string KosulluSaliverilmeOrani,
    DateOnly KosulluSaliverilmeTarihi, int DenetimliSerbestlikSuresiYil, DateOnly DenetimliSerbestlikTarihi,
    DateOnly? BihakkinTahliyeTarihi, int CezaevindeNetGun, IReadOnlyList<string> Uyarilar, string Kaynak);

// ---- calc_kidem_tazminati ----
public sealed record SeveranceRequest(DateOnly? IseGiris, DateOnly? IstenCikis, decimal BrutMaas, decimal? Yemek, decimal? Yol, decimal? Diger);
public sealed record SeveranceResponse(
    int KidemSuresiGun, int TamYil, int KalanGun, decimal GiydirilmisBrut, decimal Tavan, string TavanDonemi,
    bool TavanUygulandi, decimal EsasUcret, decimal BrutKidemTazminati, decimal DamgaVergisi, decimal NetKidemTazminati,
    string Not, string Kaynak);

// ---- calc_damga_vergisi ----
public sealed record StampDutyRequest(string KagitTuru, decimal Tutar);
public sealed record StampDutyResponse(
    string KagitTuru, decimal Tutar, decimal BindeOran, decimal HesaplananVergi, decimal AzamiSinir, bool AzamiUygulandi,
    decimal Vergi, string TarifeDonemi, string Kaynak);

// ---- calc_zamanasimi ----
public sealed record StatuteOfLimitationsRequest(string Alan, DateOnly? OlayTarihi, int? CezaUstSiniriYil, string? CezaTuru);
public sealed record StatuteOfLimitationsResponse(
    string Alan, int SureYil, DateOnly TahminiSonTarih, int? UzunSureYil, DateOnly? UzunSureSonTarih,
    string Dayanak, IReadOnlyList<string> Uyarilar, string Kaynak);

// ---- calc_tapu_harci ----
public sealed record TitleDeedFeeRequest(decimal SatisBedeli, string? Taraf);
public sealed record TitleDeedFeeResponse(
    decimal SatisBedeli, string Taraf, decimal AliciHarci, decimal SaticiHarci, decimal Harc, string Not,
    string TarifeDonemi, string Kaynak);

// ---- calc_trafik_cezasi ----
public sealed record TrafficFineRequest(string IhlalTuru, int? HizAsimKmSa, bool? YerlesimYeriIci, int? Tekrar);
public sealed record TrafficFineResponse(
    string IhlalTuru, string Madde, decimal Ceza, decimal PesinOdemeIndirimliTutar, string? EkYaptirim,
    IReadOnlyList<string> Uyarilar, string Kaynak);

// ---- calc_issizlik_odenegi ----
public sealed record UnemploymentBenefitRequest(
    bool Son120GunKesintisizCalisti, int Son3YildaPrimGunu, decimal Son4AyToplamPrimeEsasKazanc);
public sealed record UnemploymentBenefitResponse(
    bool HakKazandi, int? OdenekSuresiGun, decimal? GunlukOdenekBrut, decimal? AylikOdenekBrut,
    decimal? AylikOdenekTavani, decimal? DamgaVergisi, decimal? AylikOdenekNet, IReadOnlyList<string> Uyarilar, string Kaynak);

// ---- calc_arac_deger_kaybi ----
public sealed record VehicleValueLossRequest(decimal PiyasaDegeri, int Kilometre, string? HasarBoyutu, decimal? HasarTutari);
public sealed record VehicleValueLossResponse(
    string Yontem, decimal BazDegerKaybi, string HasarBoyutu, decimal HasarBoyutuKatsayisi, decimal KilometreKatsayisi,
    decimal DegerKaybi, string Uyari, IReadOnlyList<string> Varsayimlar, string Kaynak);

// ---- calc_is_kazasi_tazminati ----
public sealed record WorkAccidentCompensationRequest(
    string Cinsiyet, int Yas, decimal SurekliIsGoremezlikOrani, decimal? IsverenKusurOrani, decimal? AylikNetGelir,
    decimal? GeciciIsGoremezlikAy, bool SgkGelirBaglandi, decimal? SgkPesinSermaye);
public sealed record WorkAccidentCompensationResponse(
    string Yontem, decimal BakiyeOmurYil, decimal AktifDonemYil, decimal PasifDonemYil, decimal EsasAlinanAylikNetGelir,
    decimal AktifDonemKaybi, decimal PasifDonemKaybi, decimal GeciciIsGoremezlikKaybi, decimal BrutToplam,
    decimal IsverenKusurOrani, decimal KusurSonrasiTazminat, decimal SgkPesinSermayeIndirimi, decimal NetTazminat,
    string Uyari, IReadOnlyList<string> Varsayimlar, string Kaynak);

/// <summary>
/// The 27 legal/financial calculators (severance, alimony, court fees, interest, compensation,
/// tariffs...) as one service. Every method throws <see cref="Core.BadRequestException"/> for
/// invalid input — the same validation the agent tools used to return as a ToolResult error.
/// </summary>
public interface ICalculatorService
{
    AlimonyResponse CalcAlimony(AlimonyRequest request);
    AnnualLeavePayResponse CalcAnnualLeavePay(AnnualLeavePayRequest request);
    AttorneyFeeResponse CalcAttorneyFee(AttorneyFeeRequest request);
    BodilyInjuryCompensationResponse CalcBodilyInjuryCompensation(BodilyInjuryCompensationRequest request);
    CourtFeeResponse CalcCourtFee(CourtFeeRequest request);
    DeadlineResponse CalcDeadline(DeadlineRequest request);
    Task<ExchangeRatesResponse> GetExchangeRatesAsync(ExchangeRatesRequest request, CancellationToken ct = default);
    FreelanceReceiptResponse CalcFreelanceReceipt(FreelanceReceiptRequest request);
    GrossNetSalaryResponse CalcGrossNetSalary(GrossNetSalaryRequest request);
    InheritanceShareResponse CalcInheritanceShare(InheritanceShareRequest request);
    InheritanceTaxResponse CalcInheritanceTax(InheritanceTaxRequest request);
    InterestResponse CalcInterest(InterestRequest request);
    JudicialFineResponse CalcJudicialFine(JudicialFineRequest request);
    LatePaymentSurchargeResponse CalcLatePaymentSurcharge(LatePaymentSurchargeRequest request);
    LossOfSupportResponse CalcLossOfSupport(LossOfSupportRequest request);
    MediationFeeResponse CalcMediationFee(MediationFeeRequest request);
    MilitaryServiceBorrowingResponse CalcMilitaryServiceBorrowing(MilitaryServiceBorrowingRequest request);
    NoticePayResponse CalcNoticePay(NoticePayRequest request);
    OverseasBorrowingResponse CalcOverseasBorrowing(OverseasBorrowingRequest request);
    OvertimePayResponse CalcOvertimePay(OvertimePayRequest request);
    ReinstatementPayResponse CalcReinstatementPay(ReinstatementPayRequest request);
    RentIncreaseResponse CalcRentIncrease(RentIncreaseRequest request);
    RentWithholdingResponse CalcRentWithholding(RentWithholdingRequest request);
    RetirementBonusResponse CalcRetirementBonus(RetirementBonusRequest request);
    SentenceExecutionResponse CalcSentenceExecution(SentenceExecutionRequest request);
    SeveranceResponse CalcSeverance(SeveranceRequest request);
    StampDutyResponse CalcStampDuty(StampDutyRequest request);
    StatuteOfLimitationsResponse CalcStatuteOfLimitations(StatuteOfLimitationsRequest request);
    TitleDeedFeeResponse CalcTitleDeedFee(TitleDeedFeeRequest request);
    TrafficFineResponse CalcTrafficFine(TrafficFineRequest request);
    UnemploymentBenefitResponse CalcUnemploymentBenefit(UnemploymentBenefitRequest request);
    VehicleValueLossResponse CalcVehicleValueLoss(VehicleValueLossRequest request);
    WorkAccidentCompensationResponse CalcWorkAccidentCompensation(WorkAccidentCompensationRequest request);
}

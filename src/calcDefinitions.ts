// Hesap Makineleri (Araçlar > Hesap Makineleri) — backend'deki 33 ICalculatorService metodunun
// (src/shared/Korkut.Contract/Calculators/ICalculatorService.cs, /api/calc/*) UI tarafı metadata'sı.
// Her giriş burada tanımlanan `fields`'e göre TEK bir generic form (CalculatorForm.tsx) tarafından
// render edilir — 33 ayrı form komponenti yok. Alan adları (key) backend'in snake_case body
// anahtarlarıyla birebir aynıdır (Korkut.Tools/Calc/<X>Tool.cs'in JSON schema'sından alınmıştır —
// tool'lar ve REST endpoint'leri aynı Contract request tiplerini kullanır).

export type CalcFieldType = "text" | "decimal" | "int" | "enum" | "bool" | "boolOptional" | "date" | "list";

export type CalcFieldOption = { value: string; label: string };

export type CalcField = {
  key: string;
  label: string;
  type: CalcFieldType;
  /** true: boş bırakılırsa submit engellenir. false/yok: body'den tamamen atlanır (backend varsayılanı uygulanır). */
  required?: boolean;
  options?: CalcFieldOption[]; // type: "enum"
  /** type: "enum" ve seçenek değerleri aslında sayısal ise (örn. tekerrür 0/1/2) — submit'te Number()'a çevrilir. */
  numeric?: boolean;
  /** type: "list" — öğeler sayısal mı (örn. çocuk yaşları) yoksa metin mi (örn. döviz kodu). Varsayılan "text". */
  listItem?: "int" | "text";
  placeholder?: string;
  help?: string;
};

export type CalcDefinition = {
  slug: string;
  endpoint: string;
  method?: "GET";
  title: string;
  category: string;
  description: string;
  fields: CalcField[];
};

export const CALC_CATEGORIES = [
  "İş Hukuku",
  "Tazminat",
  "Aile ve Miras",
  "Yargılama ve İcra Giderleri",
  "Ceza Hukuku",
  "Vergi ve Mali",
  "Sosyal Güvenlik",
] as const;

const GENDER_OPTIONS: CalcFieldOption[] = [
  { value: "erkek", label: "Erkek" },
  { value: "kadin", label: "Kadın" },
];

const ATTORNEY_FEE_CATEGORIES: CalcFieldOption[] = [
  { value: "para_davasi", label: "Konusu para olan dava (nispi)" },
  { value: "icra_takibi", label: "İcra takibi" },
  { value: "tahliyeli_icra_takibi", label: "Tahliyeli icra takibi" },
  { value: "icra_mahkemesi_durusmali", label: "İcra mahkemesi (duruşmalı)" },
  { value: "icra_mahkemesi_durusmasiz", label: "İcra mahkemesi (duruşmasız)" },
  { value: "sulh_hukuk", label: "Sulh hukuk mahkemesi" },
  { value: "sulh_ceza_infaz", label: "Sulh ceza / infaz hakimliği" },
  { value: "asliye", label: "Asliye mahkemesi" },
  { value: "tuketici", label: "Tüketici mahkemesi" },
  { value: "fshm", label: "Fikri ve Sınai Haklar Mahkemesi" },
  { value: "idare_vergi_durusmasiz", label: "İdare/vergi mahkemesi (duruşmasız)" },
  { value: "idare_vergi_durusmali", label: "İdare/vergi mahkemesi (duruşmalı)" },
  { value: "agir_ceza", label: "Ağır ceza mahkemesi" },
  { value: "cocuk", label: "Çocuk mahkemesi" },
  { value: "cocuk_agir_ceza", label: "Çocuk ağır ceza mahkemesi" },
  { value: "bam_ilk_derece", label: "BAM ilk derece" },
  { value: "istinaf_tek_durusma", label: "İstinaf (tek duruşma)" },
  { value: "istinaf_coklu_durusma", label: "İstinaf (çoklu duruşma)" },
  { value: "yargitay_ilk_derece", label: "Yargıtay ilk derece" },
  { value: "danistay_ilk_derece_durusmasiz", label: "Danıştay ilk derece (duruşmasız)" },
  { value: "danistay_ilk_derece_durusmali", label: "Danıştay ilk derece (duruşmalı)" },
  { value: "temyiz_durusmali", label: "Temyiz (duruşmalı)" },
  { value: "uyusmazlik_mahkemesi", label: "Uyuşmazlık Mahkemesi" },
  { value: "aym_yuce_divan", label: "AYM Yüce Divan" },
  { value: "aym_bireysel_durusmasiz", label: "AYM bireysel başvuru (duruşmasız)" },
  { value: "aym_bireysel_durusmali", label: "AYM bireysel başvuru (duruşmalı)" },
  { value: "aym_diger_dava_isler", label: "AYM diğer dava ve işler" },
];

export const CALC_DEFINITIONS: CalcDefinition[] = [
  // ---- İş Hukuku ----
  {
    slug: "kidem-tazminati",
    endpoint: "/api/calc/kidem-tazminati",
    title: "Kıdem Tazminatı",
    category: "İş Hukuku",
    description: "Giydirilmiş brüt ücret, tavan kontrolü ve damga vergisiyle kıdem tazminatını hesaplar (1475 sK m.14).",
    fields: [
      { key: "ise_giris", label: "İşe giriş tarihi", type: "date", required: true },
      { key: "isten_cikis", label: "İşten çıkış (fesih) tarihi", type: "date", required: true },
      { key: "brut_maas", label: "Son aylık brüt çıplak ücret (TL)", type: "decimal", required: true },
      { key: "yemek", label: "Aylık brüt yemek yardımı (TL)", type: "decimal" },
      { key: "yol", label: "Aylık brüt yol yardımı (TL)", type: "decimal" },
      { key: "diger", label: "Diğer düzenli aylık brüt yan haklar (TL)", type: "decimal" },
    ],
  },
  {
    slug: "ihbar-tazminati",
    endpoint: "/api/calc/ihbar-tazminati",
    title: "İhbar Tazminatı",
    category: "İş Hukuku",
    description: "4857 sK m.17 bildirim sürelerine göre ihbar tazminatını (gelir + damga vergisi dahil) hesaplar.",
    fields: [
      { key: "kidem_ay", label: "Kıdem süresi (ay)", type: "decimal", help: "İşe giriş/çıkış tarihi verilmezse zorunlu" },
      { key: "ise_giris", label: "İşe giriş tarihi", type: "date", help: "kidem_ay yerine tarih çifti de girilebilir" },
      { key: "isten_cikis", label: "Fesih tarihi", type: "date" },
      { key: "brut_maas", label: "Son aylık brüt çıplak ücret (TL)", type: "decimal", required: true },
      { key: "yemek", label: "Aylık brüt yemek yardımı (TL)", type: "decimal" },
      { key: "yol", label: "Aylık brüt yol yardımı (TL)", type: "decimal" },
      { key: "diger", label: "Diğer düzenli aylık brüt yan haklar (TL)", type: "decimal" },
    ],
  },
  {
    slug: "fazla-mesai",
    endpoint: "/api/calc/fazla-mesai",
    title: "Fazla Mesai Ücreti",
    category: "İş Hukuku",
    description: "Fazla mesai, hafta tatili ve resmi tatil çalışması brüt alacaklarını hesaplar (4857 sK m.41, 46, 47).",
    fields: [
      { key: "aylik_brut_ucret", label: "Aylık brüt ücret (TL)", type: "decimal", required: true },
      { key: "fazla_mesai_saat", label: "Fazla mesai saati (haftalık 45 saati aşan)", type: "decimal" },
      { key: "hafta_tatili_saat", label: "Hafta tatilinde çalışılan saat", type: "decimal" },
      { key: "resmi_tatil_gun", label: "Resmi tatilde çalışılan gün", type: "decimal" },
    ],
  },
  {
    slug: "yillik-izin-ucreti",
    endpoint: "/api/calc/yillik-izin-ucreti",
    title: "Yıllık İzin Ücreti",
    category: "İş Hukuku",
    description: "Yıllık ücretli izin hakkını ve kullanılmayan izin ücretini hesaplar (4857 sK m.53, 57, 59).",
    fields: [
      { key: "kidem_yil", label: "Kıdem süresi (yıl)", type: "decimal", required: true, placeholder: "örn. 6.5" },
      { key: "gunluk_brut_ucret", label: "Günlük brüt çıplak ücret (TL)", type: "decimal", required: true },
      { key: "kullanilmayan_izin_gunu", label: "Kullanılmayan izin günü", type: "decimal", help: "Verilmezse kıdeme göre yıllık hak yazılır" },
      { key: "yas", label: "Yaş", type: "int", help: "18 ve altı / 50 ve üzeri için asgari 20 gün uygulanır" },
    ],
  },
  {
    slug: "ise-iade-tazminati",
    endpoint: "/api/calc/ise-iade-tazminati",
    title: "İşe İade Tazminatı",
    category: "İş Hukuku",
    description: "Boşta geçen süre ücreti ve işe başlatmama tazminatını hesaplar (4857 sK m.21).",
    fields: [
      { key: "aylik_brut", label: "Aylık brüt ücret (TL)", type: "decimal", required: true },
      { key: "kidem_yil", label: "Kıdem süresi (yıl)", type: "decimal", required: true, placeholder: "örn. 3.5" },
    ],
  },
  {
    slug: "issizlik-odenegi",
    endpoint: "/api/calc/issizlik-odenegi",
    title: "İşsizlik Ödeneği",
    category: "İş Hukuku",
    description: "Hak kazanma şartını, ödenek süresini ve brüt/net tutarını hesaplar (4447 sK m.50-51).",
    fields: [
      { key: "son_120_gun_kesintisiz_calisti", label: "Son 120 gün kesintisiz çalıştı mı", type: "bool", required: true },
      { key: "son_3_yilda_prim_gunu", label: "Son 3 yılda ödenen prim günü", type: "int", required: true },
      { key: "son_4_ay_toplam_prime_esas_kazanc", label: "Son 4 ay toplam prime esas kazanç (TL)", type: "decimal", required: true },
    ],
  },
  {
    slug: "brut-net-maas",
    endpoint: "/api/calc/brut-net-maas",
    title: "Brüt-Net Maaş",
    category: "İş Hukuku",
    description: "2026 bordro parametreleriyle brütten nete veya netten brüte maaş hesaplar.",
    fields: [
      {
        key: "yon", label: "Hesap yönü", type: "enum", required: true,
        options: [
          { value: "brutten_nete", label: "Brütten nete" },
          { value: "netten_brute", label: "Netten brüte" },
        ],
      },
      { key: "tutar", label: "Tutar (TL)", type: "decimal", required: true },
      { key: "ay", label: "Yılın ayı (1-12)", type: "int", placeholder: "1", help: "Varsayılan 1" },
    ],
  },

  // ---- Tazminat ----
  {
    slug: "bedensel-tazminat",
    endpoint: "/api/calc/bedensel-tazminat",
    title: "Bedensel Zarar Tazminatı",
    category: "Tazminat",
    description: "Yaralanmalı trafik kazasında sürekli/geçici iş göremezlik tazminatını mahkeme modunda hesaplar.",
    fields: [
      { key: "cinsiyet", label: "Cinsiyet", type: "enum", required: true, options: GENDER_OPTIONS },
      { key: "yas", label: "Kaza tarihindeki yaş", type: "int", required: true },
      { key: "maluliyet_orani", label: "Sürekli maluliyet oranı (%)", type: "decimal", required: true },
      { key: "kusur_orani", label: "Mağdurun kendi kusuru (%)", type: "decimal", help: "Verilmezse 0 alınır" },
      { key: "aylik_net_gelir", label: "Aylık net gelir (TL)", type: "decimal", help: "Verilmezse AGİ hariç net asgari ücret alınır" },
      { key: "gecici_is_goremezlik_ay", label: "Geçici iş göremezlik süresi (ay)", type: "decimal" },
    ],
  },
  {
    slug: "is-kazasi-tazminati",
    endpoint: "/api/calc/is-kazasi-tazminati",
    title: "İş Kazası Tazminatı",
    category: "Tazminat",
    description: "İş kazasında işçinin sürekli/geçici iş göremezlik (maddi) tazminatını mahkeme modunda hesaplar.",
    fields: [
      { key: "cinsiyet", label: "Cinsiyet", type: "enum", required: true, options: GENDER_OPTIONS },
      { key: "yas", label: "Kaza tarihindeki yaş", type: "int", required: true },
      { key: "surekli_is_goremezlik_orani", label: "Sürekli iş göremezlik oranı (%)", type: "decimal", required: true },
      { key: "isveren_kusur_orani", label: "İşverenin kusur oranı (%)", type: "decimal", help: "Verilmezse 100 alınır" },
      { key: "aylik_net_gelir", label: "Aylık net gelir (TL)", type: "decimal", help: "Verilmezse AGİ hariç net asgari ücret alınır" },
      { key: "gecici_is_goremezlik_ay", label: "Geçici iş göremezlik süresi (ay)", type: "decimal" },
      { key: "sgk_gelir_baglandi", label: "SGK sürekli iş göremezlik geliri bağladı mı", type: "bool", required: true },
      { key: "sgk_pesin_sermaye", label: "SGK gelirinin peşin sermaye değeri (TL)", type: "decimal", help: "Verilirse tazminattan düşülür" },
    ],
  },
  {
    slug: "destekten-yoksunluk",
    endpoint: "/api/calc/destekten-yoksunluk",
    title: "Destekten Yoksun Kalma Tazminatı",
    category: "Tazminat",
    description: "Ölümlü olayda hak sahibi bazında destekten yoksun kalma tazminatını mahkeme modunda hesaplar.",
    fields: [
      { key: "cinsiyet", label: "Müteveffanın cinsiyeti", type: "enum", required: true, options: GENDER_OPTIONS },
      { key: "yas", label: "Ölüm tarihindeki yaş", type: "int", required: true },
      { key: "aylik_net_gelir", label: "Aylık net gelir (TL)", type: "decimal", help: "Verilmezse AGİ hariç net asgari ücret alınır" },
      { key: "kusur_orani", label: "Müteveffanın kendi kusuru (%)", type: "decimal", help: "Verilmezse 0 alınır" },
      { key: "es_var", label: "Sağ kalan eş var mı", type: "bool", required: true },
      { key: "es_yasi", label: "Eşin yaşı", type: "int", help: "Verilmezse müteveffanın yaşıyla aynı varsayılır" },
      { key: "cocuklar", label: "Çocukların yaşları", type: "list", listItem: "int", placeholder: "örn. 4, 9, 15", help: "Virgülle ayırın" },
      { key: "anne_var", label: "Destek gören anne var mı", type: "bool", required: true },
      { key: "anne_yasi", label: "Annenin yaşı", type: "int", help: "Verilmezse müteveffanın yaşı + 25 varsayılır" },
      { key: "baba_var", label: "Destek gören baba var mı", type: "bool", required: true },
      { key: "baba_yasi", label: "Babanın yaşı", type: "int", help: "Verilmezse müteveffanın yaşı + 25 varsayılır" },
    ],
  },
  {
    slug: "arac-deger-kaybi",
    endpoint: "/api/calc/arac-deger-kaybi",
    title: "Araç Değer Kaybı",
    category: "Tazminat",
    description: "Kazalı aracın değer kaybını ZMSS Genel Şartları ekindeki resmi formülle hesaplar.",
    fields: [
      { key: "piyasa_degeri", label: "Aracın hasarsız rayiç değeri (TL)", type: "decimal", required: true },
      { key: "kilometre", label: "Kaza tarihindeki kilometre", type: "int", required: true },
      {
        key: "hasar_boyutu", label: "Hasar boyutu", type: "enum",
        options: [
          { value: "buyuk", label: "Büyük" },
          { value: "orta", label: "Orta" },
          { value: "kucuk", label: "Küçük" },
          { value: "basit", label: "Basit" },
        ],
        help: "Verilmezse hasar tutarından türetilir",
      },
      { key: "hasar_tutari", label: "Onarım (hasar) tutarı (TL)", type: "decimal" },
    ],
  },

  // ---- Aile ve Miras ----
  {
    slug: "nafaka",
    endpoint: "/api/calc/nafaka",
    title: "Nafaka",
    category: "Aile ve Miras",
    description: "Yoksulluk, iştirak veya tedbir nafakası için TAHMİNİ bir aralık verir.",
    fields: [
      {
        key: "nafaka_turu", label: "Nafaka türü", type: "enum", required: true,
        options: [
          { value: "yoksulluk", label: "Yoksulluk nafakası (TMK 175)" },
          { value: "istirak", label: "İştirak nafakası (TMK 182/330)" },
          { value: "tedbir", label: "Tedbir nafakası (TMK 169)" },
        ],
      },
      { key: "yukumlu_net_gelir", label: "Yükümlünün aylık net geliri (TL)", type: "decimal", required: true },
      { key: "cocuk_sayisi", label: "Çocuk sayısı", type: "int", help: "İştirak nafakası için zorunlu" },
      { key: "es_calisiyor", label: "Nafaka talep eden eş çalışıyor mu", type: "bool", required: true },
    ],
  },
  {
    slug: "miras-pay",
    endpoint: "/api/calc/miras-pay",
    title: "Miras Payı",
    category: "Aile ve Miras",
    description: "TMK zümre sistemine göre yasal miras paylarını ve saklı payları hesaplar.",
    fields: [
      { key: "es_var", label: "Sağ kalan eş var mı", type: "boolOptional" },
      { key: "cocuk_sayisi", label: "Sağ çocuk sayısı", type: "int", required: true },
      { key: "anne_var", label: "Anne sağ mı", type: "bool", required: true },
      { key: "baba_var", label: "Baba sağ mı", type: "bool", required: true },
      { key: "kardes_sayisi", label: "Kardeş sayısı", type: "int", help: "Ölen ana/babanın yerini alırlar" },
      { key: "miras_degeri", label: "Terekenin değeri (TL)", type: "decimal", help: "Girilirse TL karşılıkları da hesaplanır" },
    ],
  },
  {
    slug: "veraset-intikal",
    endpoint: "/api/calc/veraset-intikal",
    title: "Veraset ve İntikal Vergisi",
    category: "Aile ve Miras",
    description: "7338 sK'ya göre veraset veya ivazsız intikal vergisini kademeli hesaplar.",
    fields: [
      {
        key: "intikal_turu", label: "İntikal türü", type: "enum", required: true,
        options: [
          { value: "veraset", label: "Veraset (miras yoluyla)" },
          { value: "ivazsiz", label: "İvazsız (bağış vb.)" },
        ],
      },
      { key: "matrah", label: "İntikal eden değer (TL)", type: "decimal", required: true },
      { key: "es_tek_basina", label: "Füruğ yok, intikal eşe mi", type: "bool", required: true, help: "İşaretlenirse eş istisnası yükselir" },
    ],
  },

  // ---- Yargılama ve İcra Giderleri ----
  {
    slug: "harc",
    endpoint: "/api/calc/harc",
    title: "Yargı Harçları",
    category: "Yargılama ve İcra Giderleri",
    description: "Dava açılışı, ıslah, kanun yolu ve icra takibi harçlarını kalem kalem hesaplar.",
    fields: [
      {
        key: "islem", label: "İşlem", type: "enum", required: true,
        options: [
          { value: "dava_acilis", label: "Dava açılışı" },
          { value: "islah_deger_artirimi", label: "Islah / değer artırımı" },
          { value: "istinaf", label: "İstinaf" },
          { value: "temyiz", label: "Temyiz" },
          { value: "icra_takip", label: "İcra takibi" },
        ],
      },
      {
        key: "dava_turu", label: "Dava türü", type: "enum",
        options: [
          { value: "genel", label: "Genel" },
          { value: "olum_cismani", label: "Ölüm / cismani zarar" },
        ],
      },
      { key: "dava_degeri", label: "Dava değeri / takip tutarı (TL)", type: "decimal", help: "Nispi hesap ve icra takibinde zorunlu" },
      { key: "eski_deger", label: "Eski değer (TL)", type: "decimal", help: "Islah/değer artırımı için" },
      { key: "yeni_deger", label: "Yeni değer (TL)", type: "decimal", help: "Islah/değer artırımı için" },
      { key: "maktu", label: "Maktu (konusu parayla ölçülemeyen dava)", type: "bool", required: true },
      {
        key: "mahkeme", label: "Mahkeme", type: "enum",
        options: [
          { value: "sulh", label: "Sulh" },
          { value: "asliye_idare", label: "Asliye / İdare" },
        ],
        help: "Varsayılan asliye/idare",
      },
      {
        key: "tahsilat_asamasi", label: "Tahsilat aşaması", type: "enum",
        options: [
          { value: "haciz_oncesi", label: "Hacizden önce" },
          { value: "haciz_sonrasi", label: "Hacizden sonra" },
          { value: "satis_sonrasi", label: "Satıştan sonra" },
        ],
        help: "İcra takibinde tahsil harcı için",
      },
      { key: "tahsilat_tutari", label: "Tahsilat tutarı (TL)", type: "decimal", help: "İcra takibinde fiilen tahsil edilen tutar" },
    ],
  },
  {
    slug: "vekalet-ucreti",
    endpoint: "/api/calc/vekalet-ucreti",
    title: "Vekalet Ücreti",
    category: "Yargılama ve İcra Giderleri",
    description: "AAÜT 2025-2026'ya göre nispi veya maktu asgari vekalet ücretini hesaplar.",
    fields: [
      { key: "kategori", label: "Kategori", type: "enum", required: true, options: ATTORNEY_FEE_CATEGORIES },
      { key: "dava_degeri", label: "Dava değeri (TL)", type: "decimal", help: "Kategori 'konusu para olan dava' ise zorunlu" },
      {
        key: "taban_kategori", label: "Taban kategori", type: "enum",
        options: ATTORNEY_FEE_CATEGORIES.filter((o) => o.value !== "para_davasi"),
        help: "Nispi hesapta taban alınacak maktu kalem (varsayılan asliye)",
      },
    ],
  },
  {
    slug: "arabuluculuk-ucreti",
    endpoint: "/api/calc/arabuluculuk-ucreti",
    title: "Arabuluculuk Ücreti",
    category: "Yargılama ve İcra Giderleri",
    description: "2026 Arabuluculuk Asgari Ücret Tarifesi'ne göre saatlik veya anlaşma ücretini hesaplar.",
    fields: [
      {
        key: "uyusmazlik_turu", label: "Uyuşmazlık türü", type: "enum", required: true,
        options: [
          { value: "aile", label: "Aile" },
          { value: "isci_isveren", label: "İşçi-işveren" },
          { value: "tuketici", label: "Tüketici" },
          { value: "kira", label: "Kira, komşu hakkı, kat mülkiyeti" },
          { value: "ortakligin_giderilmesi", label: "Ortaklığın giderilmesi" },
          { value: "ticari", label: "Ticari" },
          { value: "tarimsal", label: "Tarımsal üretim sözleşmesi" },
          { value: "diger", label: "Diğer" },
        ],
      },
      { key: "anlasma", label: "Süreç anlaşmayla mı sonuçlandı", type: "bool", required: true },
      { key: "anlasma_tutari", label: "Anlaşma tutarı (TL)", type: "decimal", help: "Anlaşma varsa" },
      { key: "taraf_sayisi", label: "Taraf sayısı", type: "int", help: "Varsayılan 2" },
      { key: "saat", label: "Harcanan saat", type: "int", help: "Anlaşma yoksa" },
      { key: "arabulucu_sayisi", label: "Arabulucu sayısı", type: "int", help: "Varsayılan 1" },
      { key: "dava_sarti", label: "Dava şartı arabuluculuk mu", type: "boolOptional", help: "Varsayılan evet; hayırsa anlaşma halinde asgari ücret tabanı uygulanmaz" },
    ],
  },
  {
    slug: "sure",
    endpoint: "/api/calc/sure",
    title: "Usul Süresi Hesabı",
    category: "Yargılama ve İcra Giderleri",
    description: "Kanun yolu, cevap ve icra itiraz sürelerinin son gününü tatil kurallarıyla hesaplar.",
    fields: [
      {
        key: "sure_turu", label: "Süre türü", type: "enum", required: true,
        options: [
          { value: "istinaf_hukuk", label: "İstinaf (hukuk)" },
          { value: "istinaf_ceza", label: "İstinaf (ceza)" },
          { value: "temyiz_hukuk", label: "Temyiz (hukuk)" },
          { value: "temyiz_ceza", label: "Temyiz (ceza)" },
          { value: "cevap_dilekcesi", label: "Cevap dilekçesi" },
          { value: "icra_itiraz", label: "İcra takibine itiraz" },
          { value: "itirazin_iptali", label: "İtirazın iptali davası" },
          { value: "istinaf_idare", label: "İstinaf (idare)" },
          { value: "temyiz_idare", label: "Temyiz (idare)" },
        ],
      },
      { key: "teblig_tarihi", label: "Tebliğ tarihi", type: "date", required: true },
    ],
  },
  {
    slug: "zamanasimi",
    endpoint: "/api/calc/zamanasimi",
    title: "Zamanaşımı",
    category: "Yargılama ve İcra Giderleri",
    description: "TBK alacak zamanaşımları ile TCK dava ve ceza infaz zamanaşımını hesaplar.",
    fields: [
      {
        key: "alan", label: "Alan", type: "enum", required: true,
        options: [
          { value: "tbk_genel", label: "TBK genel alacak" },
          { value: "haksiz_fiil", label: "Haksız fiil" },
          { value: "kira", label: "Kira / dönemsel edim" },
          { value: "ticari_satis", label: "Ticari satış" },
          { value: "ceza_dava", label: "Ceza davası (TCK m.66)" },
          { value: "ceza_infaz", label: "Ceza infazı (TCK m.68)" },
        ],
      },
      { key: "olay_tarihi", label: "Olay / muacceliyet tarihi", type: "date", required: true },
      { key: "ceza_ust_siniri_yil", label: "Cezanın kanuni üst sınırı / hükmedilen ceza (yıl)", type: "int", help: "Ceza alanlarında süreli hapiste zorunlu" },
      {
        key: "ceza_turu", label: "Ceza türü", type: "enum",
        options: [
          { value: "sureli", label: "Süreli" },
          { value: "muebbet", label: "Müebbet" },
          { value: "agirlastirilmis_muebbet", label: "Ağırlaştırılmış müebbet" },
        ],
        help: "Ceza alanlarında; varsayılan süreli",
      },
    ],
  },

  // ---- Ceza Hukuku ----
  {
    slug: "adli-para-cezasi",
    endpoint: "/api/calc/adli-para-cezasi",
    title: "Adli Para Cezası",
    category: "Ceza Hukuku",
    description: "Gün sayısı x günlük miktar üzerinden adli para cezasını ve hapse çevirme karşılığını hesaplar.",
    fields: [
      { key: "gun_sayisi", label: "Gün sayısı (TCK m.52/1: 5-730)", type: "int", required: true },
      { key: "gunluk_miktar", label: "Günlük miktar (TL)", type: "decimal", required: true },
      { key: "taksit", label: "Taksitle ödeme bilgisi de istensin mi", type: "bool", required: true },
    ],
  },
  {
    slug: "infaz",
    endpoint: "/api/calc/infaz",
    title: "Ceza İnfaz Hesabı",
    category: "Ceza Hukuku",
    description: "Koşullu salıverilme, denetimli serbestlik ve bihakkın tahliye tarihlerini hesaplar.",
    fields: [
      { key: "suc_tarihi", label: "Suç tarihi", type: "date", required: true },
      { key: "dogum_tarihi", label: "Hükümlünün doğum tarihi", type: "date", help: "Suç tarihinde 18 yaş altı kontrolü için" },
      {
        key: "ceza_turu", label: "Ceza türü", type: "enum", required: true,
        options: [
          { value: "sureli", label: "Süreli" },
          { value: "muebbet", label: "Müebbet" },
          { value: "agirlastirilmis_muebbet", label: "Ağırlaştırılmış müebbet" },
        ],
      },
      { key: "ceza_yil", label: "Ceza — yıl", type: "int", help: "Süreli hapiste" },
      { key: "ceza_ay", label: "Ceza — ay", type: "int", help: "Süreli hapiste" },
      { key: "ceza_gun", label: "Ceza — gün", type: "int", help: "Süreli hapiste" },
      {
        key: "suc_tipi", label: "Suç tipi", type: "enum", required: true,
        options: [
          { value: "adi", label: "Adi (genel rejim 1/2)" },
          { value: "kasten_oldurme", label: "Kasten öldürme" },
          { value: "cinsel", label: "Cinsel suçlar" },
          { value: "uyusturucu_ticareti", label: "Uyuşturucu ticareti" },
          { value: "teror_orgutlu", label: "Terör / örgütlü suç" },
        ],
      },
      {
        key: "tekerrur", label: "Tekerrür durumu", type: "enum", numeric: true,
        options: [
          { value: "0", label: "Yok" },
          { value: "1", label: "Birinci mükerrir" },
          { value: "2", label: "İkinci mükerrir" },
        ],
      },
      { key: "infaz_baslangic", label: "İnfaz başlangıcı (cezaevine giriş)", type: "date", required: true },
      { key: "mahsup_gun", label: "Mahsup edilecek gün (TCK m.63)", type: "int" },
      {
        key: "ozel_durum", label: "Özel durum", type: "enum",
        options: [
          { value: "yok", label: "Yok" },
          { value: "cocuklu_kadin", label: "0-6 yaş çocuklu kadın" },
          { value: "agir_hasta_yasli", label: "Ağır hasta / yaşlı" },
        ],
      },
    ],
  },
  {
    slug: "trafik-cezasi",
    endpoint: "/api/calc/trafik-cezasi",
    title: "Trafik Cezası",
    category: "Ceza Hukuku",
    description: "2026 trafik idari para cezalarını ihlal türüne göre, peşin ödeme indirimiyle hesaplar.",
    fields: [
      {
        key: "ihlal_turu", label: "İhlal türü", type: "enum", required: true,
        options: [
          { value: "hiz", label: "Hız aşımı" },
          { value: "emniyet_kemeri", label: "Emniyet kemeri" },
          { value: "alkol", label: "Alkollü araç kullanma" },
          { value: "kirmizi_isik", label: "Kırmızı ışık" },
          { value: "cep_telefonu", label: "Cep telefonuyla konuşma" },
          { value: "trafik_sigortasi_yok", label: "Trafik sigortası yok" },
          { value: "muayenesiz", label: "Muayenesiz araç" },
        ],
      },
      { key: "hiz_asim_km_sa", label: "Hız aşımı (km/s)", type: "int", help: "Hız ihlali için zorunlu" },
      { key: "yerlesim_yeri_ici", label: "Yerleşim yeri içi mi", type: "boolOptional", help: "Hız ihlali için; varsayılan evet" },
      { key: "tekrar", label: "Kaçıncı tespit", type: "int", help: "Alkol / kırmızı ışık / cep telefonu / muayenesiz için; varsayılan 1" },
    ],
  },

  // ---- Vergi ve Mali ----
  {
    slug: "smm",
    endpoint: "/api/calc/smm",
    title: "Serbest Meslek Makbuzu",
    category: "Vergi ve Mali",
    description: "Brüt, net veya KDV dahil tutardan stopaj, KDV ve tahsil edilecek toplamı hesaplar.",
    fields: [
      {
        key: "yon", label: "Verilen tutarın anlamı", type: "enum", required: true,
        options: [
          { value: "brutten", label: "Brüt ücret" },
          { value: "netten", label: "Net ele geçen" },
          { value: "kdv_dahilden", label: "KDV dahil toplam" },
        ],
      },
      { key: "tutar", label: "Tutar (TL)", type: "decimal", required: true },
      { key: "kdv_orani", label: "KDV oranı (%)", type: "decimal", help: "Varsayılan 20" },
      { key: "stopaj_orani", label: "Stopaj oranı (%)", type: "decimal", help: "Varsayılan 20" },
      { key: "kdv_tevkifati", label: "5/10 KDV tevkifatı uygulansın mı", type: "bool", required: true },
    ],
  },
  {
    slug: "damga-vergisi",
    endpoint: "/api/calc/damga-vergisi",
    title: "Damga Vergisi",
    category: "Vergi ve Mali",
    description: "488 sK'ya göre kağıt türüne göre binde oranla damga vergisini hesaplar.",
    fields: [
      {
        key: "kagit_turu", label: "Kağıt türü", type: "enum", required: true,
        options: [
          { value: "sozlesme", label: "Sözleşme / mukavelename / taahhütname (binde 9,48)" },
          { value: "kira_sozlesmesi", label: "Kira sözleşmesi (binde 1,89)" },
          { value: "ihale_karari", label: "İhale kararı (binde 5,69)" },
          { value: "ucret_odemesi", label: "Ücret ödemesi (binde 7,59)" },
          { value: "fesihname", label: "Fesihname (binde 1,89)" },
        ],
      },
      { key: "tutar", label: "Tutar (TL)", type: "decimal", required: true },
    ],
  },
  {
    slug: "tapu-harci",
    endpoint: "/api/calc/tapu-harci",
    title: "Tapu Harcı",
    category: "Vergi ve Mali",
    description: "Gayrimenkul satışında alıcı ve satıcı tapu harcını hesaplar (492 sK 4 sayılı tarife I-20/a).",
    fields: [
      { key: "satis_bedeli", label: "Satış bedeli (TL)", type: "decimal", required: true, help: "Emlak vergisi değerinden düşük olamaz" },
      {
        key: "taraf", label: "Taraf", type: "enum",
        options: [
          { value: "alici", label: "Alıcı" },
          { value: "satici", label: "Satıcı" },
          { value: "toplam", label: "Toplam" },
        ],
        help: "Varsayılan toplam",
      },
    ],
  },
  {
    slug: "kira-stopaji",
    endpoint: "/api/calc/kira-stopaji",
    title: "Kira Stopajı",
    category: "Vergi ve Mali",
    description: "GVK m.94/5-a'ya göre işyeri kira stopajını (brüt↔net) hesaplar.",
    fields: [
      {
        key: "yon", label: "Hesap yönü", type: "enum", required: true,
        options: [
          { value: "brutten_nete", label: "Brütten nete" },
          { value: "netten_brute", label: "Netten brüte" },
        ],
      },
      { key: "tutar", label: "Tutar (TL)", type: "decimal", required: true },
      { key: "odeyen_stopaja_tabi", label: "Ödeyen stopaj yükümlüsü mü", type: "bool", required: true, help: "Şirket/kurum vb. — değilse stopaj uygulanmaz" },
    ],
  },
  {
    slug: "kira-artis",
    endpoint: "/api/calc/kira-artis",
    title: "Kira Artış Sınırı",
    category: "Vergi ve Mali",
    description: "TBK m.344 kira artışı üst sınırını (TÜFE 12 aylık ortalamaya göre) hesaplar.",
    fields: [
      { key: "mevcut_kira", label: "Mevcut aylık kira (TL)", type: "decimal", required: true },
      { key: "yenileme_ayi", label: "Yenileme ayı", type: "text", required: true, placeholder: "2026-07", help: "YYYY-MM biçiminde" },
      { key: "tufe_orani", label: "TÜFE oranı (%)", type: "decimal", help: "Verilmezse gömülü TÜİK tablosundan bulunur" },
    ],
  },
  {
    slug: "faiz",
    endpoint: "/api/calc/faiz",
    title: "Faiz Hesabı",
    category: "Vergi ve Mali",
    description: "Yasal faiz, avans (ticari temerrüt) faizi veya özel oranla basit faiz hesaplar.",
    fields: [
      {
        key: "faiz_turu", label: "Faiz türü", type: "enum", required: true,
        options: [
          { value: "yasal", label: "Yasal (3095 m.1)" },
          { value: "avans", label: "Avans (ticari temerrüt)" },
          { value: "ozel", label: "Özel oran" },
        ],
      },
      { key: "ana_para", label: "Ana para (TL)", type: "decimal", required: true },
      { key: "baslangic", label: "Başlangıç (temerrüt) tarihi", type: "date", required: true },
      { key: "bitis", label: "Bitiş (hesap sonu) tarihi", type: "date", required: true },
      { key: "bitis_dahil", label: "Bitiş günü de faize dahil edilsin mi", type: "bool", required: true },
      { key: "ozel_oran", label: "Özel yıllık oran (%)", type: "decimal", help: "faiz_turu=özel iken" },
    ],
  },
  {
    slug: "gecikme-zammi",
    endpoint: "/api/calc/gecikme-zammi",
    title: "Gecikme Zammı",
    category: "Vergi ve Mali",
    description: "6183 sayılı AATUHK m.51'e göre vergi/SGK ve diğer amme alacaklarında gecikme zammı hesaplar.",
    fields: [
      { key: "ana_para", label: "Amme alacağının aslı (TL)", type: "decimal", required: true },
      { key: "vade_tarihi", label: "Vade (son ödeme) tarihi", type: "date", required: true },
      { key: "odeme_tarihi", label: "Fiili ödeme / hesap tarihi", type: "date", required: true },
    ],
  },
  {
    slug: "doviz-kurlari",
    endpoint: "/api/calc/doviz-kurlari",
    method: "GET",
    title: "Döviz Kurları",
    category: "Vergi ve Mali",
    description: "TCMB günlük gösterge döviz alış/satış kurlarını getirir.",
    fields: [
      { key: "para_birimleri", label: "Para birimleri (ISO kodu)", type: "list", listItem: "text", placeholder: "USD, EUR, GBP", help: "Virgülle ayırın; verilmezse USD, EUR, GBP" },
      { key: "tarih", label: "Kur tarihi", type: "date", help: "Verilmezse en güncel bülten" },
    ],
  },

  // ---- Sosyal Güvenlik ----
  {
    slug: "askerlik-borclanmasi",
    endpoint: "/api/calc/askerlik-borclanmasi",
    title: "Askerlik Borçlanması",
    category: "Sosyal Güvenlik",
    description: "5510 sK m.41'e göre askerlik borçlanması tutarını hesaplar.",
    fields: [
      { key: "gun_sayisi", label: "Borçlanılacak gün sayısı", type: "int", required: true },
      { key: "gunluk_tutar", label: "Günlük tutar (TL)", type: "decimal", help: "Verilmezse günlük alt sınır (asgari ücret) kullanılır" },
    ],
  },
  {
    slug: "yurtdisi-borclanma",
    endpoint: "/api/calc/yurtdisi-borclanma",
    title: "Yurtdışı Hizmet Borçlanması",
    category: "Sosyal Güvenlik",
    description: "3201 sK'ya göre yurtdışında geçen sürelerin borçlanma tutarını hesaplar.",
    fields: [
      { key: "gun_sayisi", label: "Borçlanılacak gün sayısı", type: "int", required: true },
      { key: "gunluk_tutar", label: "Günlük tutar (TL)", type: "decimal", help: "Verilmezse günlük alt sınır (asgari ücret) kullanılır" },
    ],
  },
  {
    slug: "emeklilik-ikramiyesi",
    endpoint: "/api/calc/emeklilik-ikramiyesi",
    title: "Emeklilik İkramiyesi",
    category: "Sosyal Güvenlik",
    description: "5434 sK m.89'a göre memur emekli ikramiyesini (EKEAT x tam hizmet yılı) hesaplar.",
    fields: [
      { key: "ekeat_brut", label: "EKEAT (TL)", type: "decimal", required: true, help: "Emekli keseneğine esas aylık tutarı — brüt maaş değildir, bordro/e-Devlet'ten alınır" },
      { key: "hizmet_yili", label: "Toplam hizmet süresi (yıl)", type: "decimal", required: true, placeholder: "örn. 25.5" },
    ],
  },
];

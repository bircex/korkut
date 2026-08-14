import {
  type Icon,
  BookmarkSimple,
  BookOpenText,
  Calculator,
  CalendarBlank,
  CheckSquare,
  FileText,
  Folder,
  Scales,
  Users,
  Wallet,
  Wrench,
} from "../icons";

// Sohbette gösterilen araç adları: LLM'e giden ham fonksiyon adı (ör. "search_decisions") hiçbir
// zaman kullanıcıya gösterilmez — Korkut.Tools'taki her IAgentTool için burada bir Türkçe, şimdiki
// zaman etiketi tanımlanır ("Karar aranıyor"). Yeni bir araç eklenince burada karşılığı unutulursa
// UI çökmesin diye humanize() ham adı okunur bir hale getirip döner (bkz. toolLabel altta).

const TOOL_LABELS: Record<string, string> = {
  // Hukuki arama / belge (Korkut.Tools/Law)
  search_decisions: "Karar aranıyor",
  fetch_document: "Karar getiriliyor",
  search_within_mevzuat: "Mevzuat metninde aranıyor",
  search_within_sigorta_tahkim: "Sigorta tahkim kararlarında aranıyor",
  get_mevzuat_madde_tree: "Mevzuat madde listesi getiriliyor",
  get_mevzuat_gerekce: "Kanun gerekçesi getiriliyor",

  // Hesaplama (Korkut.Tools/Calc)
  calc_nafaka: "Nafaka hesaplanıyor",
  calc_yillik_izin_ucreti: "Yıllık izin ücreti hesaplanıyor",
  calc_vekalet_ucreti: "Vekalet ücreti hesaplanıyor",
  calc_bedensel_tazminat: "Bedensel tazminat hesaplanıyor",
  calc_harc: "Yargı harcı hesaplanıyor",
  calc_sure: "Süre hesaplanıyor",
  get_exchange_rates: "Döviz kurları getiriliyor",
  calc_smm: "Serbest meslek makbuzu hesaplanıyor",
  calc_brut_net_maas: "Maaş hesaplanıyor",
  calc_miras_pay: "Miras payı hesaplanıyor",
  calc_veraset_intikal: "Veraset ve intikal vergisi hesaplanıyor",
  calc_faiz: "Faiz hesaplanıyor",
  calc_adli_para_cezasi: "Adli para cezası hesaplanıyor",
  calc_gecikme_zammi: "Gecikme zammı hesaplanıyor",
  calc_destekten_yoksunluk: "Destekten yoksun kalma tazminatı hesaplanıyor",
  calc_arabuluculuk_ucreti: "Arabuluculuk ücreti hesaplanıyor",
  calc_askerlik_borclanmasi: "Askerlik borçlanması hesaplanıyor",
  calc_ihbar_tazminati: "İhbar tazminatı hesaplanıyor",
  calc_yurtdisi_borclanma: "Yurtdışı borçlanma hesaplanıyor",
  calc_fazla_mesai: "Fazla mesai alacağı hesaplanıyor",
  calc_ise_iade_tazminati: "İşe iade tazminatı hesaplanıyor",
  calc_kira_artis: "Kira artış oranı hesaplanıyor",
  calc_kira_stopaji: "Kira stopajı hesaplanıyor",
  calc_emeklilik_ikramiyesi: "Emeklilik ikramiyesi hesaplanıyor",
  calc_infaz: "İnfaz hesabı yapılıyor",
  calc_kidem_tazminati: "Kıdem tazminatı hesaplanıyor",
  calc_damga_vergisi: "Damga vergisi hesaplanıyor",
  calc_zamanasimi: "Zamanaşımı süresi hesaplanıyor",
  calc_tapu_harci: "Tapu harcı hesaplanıyor",
  calc_trafik_cezasi: "Trafik cezası hesaplanıyor",
  calc_issizlik_odenegi: "İşsizlik ödeneği hesaplanıyor",
  calc_arac_deger_kaybi: "Araç değer kaybı hesaplanıyor",
  calc_is_kazasi_tazminati: "İş kazası tazminatı hesaplanıyor",

  // Büro / CRM (Korkut.Tools/Office)
  add_deadline: "Süre ekleniyor",
  add_evrak: "Evrak kaydediliyor",
  add_expense: "Masraf ekleniyor",
  add_hearing: "Duruşma ekleniyor",
  add_invoice: "Fatura ekleniyor",
  add_note: "Not ekleniyor",
  add_task: "Görev ekleniyor",
  complete_task: "Görev tamamlanıyor",
  create_client: "Müvekkil ekleniyor",
  create_event: "Randevu ekleniyor",
  create_matter: "Dosya açılıyor",
  finance_summary: "Mali özet hazırlanıyor",
  get_matter: "Dosya bilgisi getiriliyor",
  list_clients: "Müvekkiller listeleniyor",
  list_deadlines: "Süreler listeleniyor",
  list_events: "Takvim getiriliyor",
  list_evrak: "Evrak kayıtları getiriliyor",
  list_expenses: "Masraflar listeleniyor",
  list_invoices: "Faturalar listeleniyor",
  list_matters: "Dosyalar listeleniyor",
  list_members: "Büro üyeleri getiriliyor",
  list_notes: "Notlar getiriliyor",
  list_tasks: "Görevler listeleniyor",
  office_overview: "Büro özeti hazırlanıyor",
  upcoming_agenda: "Yaklaşan ajanda getiriliyor",

  // Belge / dilekçe
  draft_document: "Taslak hazırlanıyor",

  // Diğer
  search_office_documents: "Büro belgelerinde aranıyor",
  remember: "Not hatırlanıyor",
};

// Her kategoriye bir ikon: aracın "ne yaptığını" bir bakışta ayırt ettirir (bkz. Workspace.tsx'teki
// İçtihat rozetiyle aynı Scales kullanımı — tutarlılık için).
const LAW_TOOLS = [
  "search_decisions",
  "fetch_document",
  "search_within_mevzuat",
  "search_within_sigorta_tahkim",
  "get_mevzuat_madde_tree",
  "get_mevzuat_gerekce",
];
const CLIENT_TOOLS = ["create_client", "list_clients", "list_members"];
const MATTER_TOOLS = ["create_matter", "get_matter", "list_matters"];
const FINANCE_TOOLS = ["add_expense", "add_invoice", "list_expenses", "list_invoices", "finance_summary"];
const TASK_TOOLS = ["add_task", "complete_task", "list_tasks"];
const CALENDAR_TOOLS = ["add_deadline", "list_deadlines", "add_hearing", "create_event", "list_events", "upcoming_agenda"];
const NOTE_TOOLS = ["add_evrak", "list_evrak", "add_note", "list_notes", "office_overview", "remember"];
const CALC_TOOLS = Object.keys(TOOL_LABELS).filter((n) => n.startsWith("calc_"));

const TOOL_ICONS: Record<string, Icon> = Object.fromEntries([
  ...LAW_TOOLS.map((n) => [n, Scales]),
  ...CALC_TOOLS.map((n) => [n, Calculator]),
  ["get_exchange_rates", Calculator],
  ...CLIENT_TOOLS.map((n) => [n, Users]),
  ...MATTER_TOOLS.map((n) => [n, Folder]),
  ...FINANCE_TOOLS.map((n) => [n, Wallet]),
  ...TASK_TOOLS.map((n) => [n, CheckSquare]),
  ...CALENDAR_TOOLS.map((n) => [n, CalendarBlank]),
  ...NOTE_TOOLS.map((n) => [n, BookmarkSimple]),
  ["draft_document", FileText],
  ["search_office_documents", BookOpenText],
]);

/** Ham araç adına göre kategorisini yansıtan ikon; haritada yoksa jenerik araç ikonu (Wrench). */
export function toolIcon(name: string): Icon {
  return TOOL_ICONS[name] ?? Wrench;
}

function humanize(name: string): string {
  const words = name.split("_").filter(Boolean);
  if (words.length === 0) return name;
  return words[0][0].toLocaleUpperCase("tr-TR") + words[0].slice(1) + (words.length > 1 ? " " + words.slice(1).join(" ") : "");
}

/** Ham araç adını sohbette gösterilecek Türkçe etikete çevirir; haritada yoksa ham adı okunur biçime sokar. */
export function toolLabel(name: string): string {
  return TOOL_LABELS[name] ?? humanize(name);
}

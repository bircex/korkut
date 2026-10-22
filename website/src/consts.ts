// Merkezî site sabitleri: SEO ve içerik burada tek yerden yönetilir.
// Kendi alan adınıza göre `url` değerini güncelleyin.

export const SITE = {
  // Sitenin yayınlandığı origin (özel alan adı, apex). astro.config.mjs'te base "/".
  url: "https://korkutai.com",
  name: "Korkut",
  title: "Korkut: Hukuk büroları için açık kaynak yapay zeka asistanı",
  description:
    "Korkut, hukuk büroları için açık kaynak (MIT) yapay zeka asistanıdır. Kendi makinenize kurulur; içtihat ve mevzuat araştırır, dilekçe yazar, dosya, duruşma ve süreleri takip eder. Yerel bir modelle çalıştırırsanız hiçbir veri büronuzdan çıkmaz.",
  locale: "tr_TR",
  lang: "tr",
  author: "Korkut",
  keywords:
    "açık kaynak hukuk yapay zeka, avukat asistanı, kendi sunucunda yapay zeka, yerel model, LM Studio, Ollama, içtihat arama, mevzuat arama, dilekçe taslağı, Yargıtay, Danıştay, KVKK, MIT lisans",
} as const;

// Açık kaynak proje: kaynak kodu, kurulum belgesi, katkı ve güvenlik bildirimi.
export const REPO_URL = "https://github.com/bircex/korkut";
export const DOCS_URL = `${REPO_URL}/blob/main/docs/INSTALL.md`;
export const ISSUES_URL = `${REPO_URL}/issues`;
export const LICENSE_URL = `${REPO_URL}/blob/main/LICENSE`;
export const CONTRIBUTING_URL = `${REPO_URL}/blob/main/CONTRIBUTING.md`;
export const SECURITY_POLICY_URL = `${REPO_URL}/blob/main/SECURITY.md`;
export const RELEASES_URL = `${REPO_URL}/releases`;
// Her sürümün kurulum paketi aynı adı taşır, bu yüzden en yenisi hep bu adreste durur.
export const LATEST_RELEASE_URL = `${RELEASES_URL}/latest`;
export const LATEST_BUNDLE_URL = `${RELEASES_URL}/latest/download/korkut.tar.gz`;

// Kurulumdan sonra uygulamanın açıldığı yerel adres.
export const LOCAL_APP_URL = "http://localhost:8090";

// İletişim.
export const CONTACT_EMAIL = "iletisim@korkutai.com";

// Eski "/demo" (bulut dönemi kayıt sayfası) bağlantıları kırılmasın diye kurulum bölümüne yönlenir.
export const DEMO_PATH = "demo";

// Sıkça sorulan sorular: hem FAQ bölümü hem de FAQPage yapılandırılmış verisi.
export const FAQS = [
  {
    q: "Korkut ücretli mi?",
    a: "Hayır. Korkut MIT lisanslı açık kaynak bir projedir; indirip kurmak, kullanmak, değiştirmek ve büronuzda çalıştırmak ücretsizdir. Abonelik veya kullanıcı başı ücret yoktur. Bulut tabanlı bir yapay zeka modeli seçerseniz o sağlayıcının ücretlerini siz ödersiniz.",
  },
  {
    q: "Verilerim nerede tutuluyor?",
    a: "Tamamen sizin makinenizde. Korkut büronuzun kendi bilgisayarına veya sunucusuna kurulur; dosyalar, müvekkiller, belgeler ve sohbetler orada, kendi veritabanınızda durur. Proje ekibinin kurulumunuza veya verilerinize hiçbir erişimi yoktur.",
  },
  {
    q: "Kurulum için ne gerekiyor?",
    a: "Docker (Compose v2 ile) yeterli. Son sürümü indirip tek komutla kurarsınız: macOS ve Linux'ta ./install.sh, Windows'ta .\\install.ps1. Kurulum yönetici hesabının ilk parolasını üretir ve uygulamayı http://localhost:8090 adresinde açar.",
  },
  {
    q: "Hangi yapay zeka modelleriyle çalışıyor?",
    a: "Seçim sizin. Ayarlar → Yapay zeka'dan LM Studio veya Ollama gibi yerel, OpenAI uyumlu bir modele bağlanırsanız hiçbir metin makinenizden çıkmaz. Dilerseniz OpenAI, Anthropic veya Google Gemini gibi bir bulut modeli de seçebilirsiniz; bu durumda modele giden metin önce en iyi çaba ile rumuzlanır.",
  },
  {
    q: "Büroda herkes her şeyi görebilir mi?",
    a: "Hayır. Yetkiler rol ve dosya bazlıdır: avukat, stajyer, sekreter, muhasebe ve okuyucu gibi hazır roller vardır, kendi rollerinizi de tanımlayabilirsiniz. Herkes yalnızca ekibinde olduğu dosyaları görür; müvekkil iletişim ve kimlik bilgileri ile mali kayıtlar ayrıca izne bağlıdır.",
  },
  {
    q: "Korkut hukuki tavsiye veriyor mu?",
    a: "Hayır. Korkut hukuki danışmanlık veya temsil sağlamaz. Araştırma, özet, dilekçe ve sözleşme taslağı dahil her çıktı, nitelikli bir avukatın gözden geçirmesi gereken bir taslaktır. Yazılım olduğu gibi, hiçbir garanti olmaksızın sunulur.",
  },
] as const;

// Hukuki metinlerin son güncelleme tarihi (her metin değişikliğinde güncellenir).
export const LEGAL_UPDATED = "21 Ekim 2026";

// Navigasyon bağlantıları. "#..." olanlar ana sayfa bölümleridir (alt sayfalarda
// otomatik olarak "/#..." biçimine çözülür); diğerleri ayrı sayfa yollarıdır.
export const NAV_LINKS = [
  { href: "#ozellikler", label: "Özellikler" },
  { href: "#guvenlik", label: "Güvenlik" },
  { href: "#kurulum", label: "Kurulum" },
  { href: "dokumantasyon/", label: "Dokümantasyon" },
  { href: "#sss", label: "SSS" },
] as const;

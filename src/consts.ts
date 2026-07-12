// Merkezî site sabitleri: SEO ve içerik burada tek yerden yönetilir.
// Kendi alan adınıza göre `url` değerini güncelleyin.

export const SITE = {
  // Sitenin yayınlandığı origin (özel alan adı, apex). astro.config.mjs'te base "/".
  url: "https://korkutai.com",
  name: "Korkut",
  title: "Korkut: Avukatlar için yapay zeka asistanı",
  description:
    "Avukatlar ve hukuk büroları için Korkut; içtihat ve mevzuat araştırır, dilekçe yazar, dosya, duruşma ve bütçeyi takip eder, yazışmaları yürütür. Bulutta her büro tamamen izole ve verileriniz güvende kalır.",
  locale: "tr_TR",
  lang: "tr",
  author: "Korkut",
  keywords:
    "hukuk yapay zeka, avukat asistanı, içtihat arama, mevzuat arama, dilekçe taslağı, Yargıtay, Danıştay, bulut hukuk asistanı, kurumsal hukuk yapay zeka, KVKK",
} as const;

// Uygulama girişi (bulut): Google ile giriş app subdomain'inde yapılır.
export const APP_URL = "https://app.korkutai.com";

// İletişim: büro erişimi demo talebi veya iletişim ile açılır.
export const CONTACT_EMAIL = "iletisim@korkutai.com";

// Demo talep formu (ayrı sayfa) ve e-posta yedeği.
export const DEMO_PATH = "demo";
export const DEMO_MAILTO = `mailto:${CONTACT_EMAIL}?subject=${encodeURIComponent(
  "Korkut demo talebi",
)}`;

// Sıkça sorulan sorular: hem FAQ bölümü hem de FAQPage yapılandırılmış verisi.
export const FAQS = [
  {
    q: "Verilerim nerede tutuluyor?",
    a: "Her büro veritabanı düzeyinde tamamen izole edilir (satır düzeyi güvenlik ile) ve hassas alanlar şifrelenir; hiçbir büro bir diğerinin verisini göremez.",
  },
  {
    q: "Nasıl başlıyorum, kuruluma gerek var mı?",
    a: "Bulutta teknik kurulum yoktur: demo sonrası büronuza bir davet bağlantısı iletilir, Google ile giriş yaparak dakikalar içinde başlarsınız. Bakım ve güncellemeler bize aittir.",
  },
  {
    q: "Korkut hangi yapay zeka modellerini kullanıyor?",
    a: "Korkut, hukuki işe uygun üst düzey modellerle çalışacak biçimde yapılandırılır. Modele giden metinde isim, T.C. kimlik no ve dosya no gibi bilgilerin yerine önce en iyi çaba ile takma değer konur.",
  },
  {
    q: "Verilerim güvende mi?",
    a: "Modele giden metin önce en iyi çaba ile rumuzlanır ve her büro veritabanında izole tutulur. Rumuzlama azaltıcı bir önlemdir, garanti değildir; sınır ötesi veri aktarımı (KVKK md. 9) sorumluluğu size aittir.",
  },
  {
    q: "Korkut hukuki tavsiye veriyor mu?",
    a: "Hayır. Korkut hukuki danışmanlık veya temsil sağlamaz. Araştırma, özet, dilekçe ve sözleşme taslağı dahil her çıktı, nitelikli bir avukatın gözden geçirmesi gereken bir taslaktır. Çıktının doğruluğunu teyit etmek kullanıcının sorumluluğundadır.",
  },
] as const;

// Navigasyon bağlantıları. "#..." olanlar ana sayfa bölümleridir (alt sayfalarda
// otomatik olarak "/#..." biçimine çözülür); diğerleri ayrı sayfa yollarıdır.
export const NAV_LINKS = [
  { href: "#ozellikler", label: "Özellikler" },
  { href: "#entegrasyonlar", label: "Entegrasyonlar" },
  { href: "#kaynaklar", label: "Veri kaynakları" },
  { href: "#guvenlik", label: "Güvenlik" },
  { href: "#sss", label: "SSS" },
] as const;

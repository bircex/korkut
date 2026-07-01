// Merkezî site sabitleri — SEO ve içerik burada tek yerden yönetilir.
// Kendi alan adınıza göre `url` değerini güncelleyin.

export const SITE = {
  url: "https://korkut.dev",
  name: "Korkut",
  title: "Korkut — Hukuk büronuz için açık kaynak yapay zeka asistanı",
  description:
    "Korkut; içtihat ve mevzuat araştırması yapar, dilekçe taslakları hazırlar ve büronun yazışmalarını yürütür. Kendi sunucunuzda çalışır, veriler ofiste kalır. MIT lisanslı, açık kaynak.",
  locale: "tr_TR",
  lang: "tr",
  author: "Recep Kızılarslan",
  keywords:
    "hukuk yapay zeka, avukat asistanı, içtihat arama, mevzuat arama, dilekçe taslağı, Yargıtay, Danıştay, self-hosted hukuk yazılımı, açık kaynak hukuk asistanı, KVKK",
} as const;

export const GITHUB_URL = "https://github.com/recepkizilarslan/korkut";
export const CONTACT_EMAIL = "recepkizilarslan@gmail.com";
export const DOCS_URL = `${GITHUB_URL}/tree/main/docs`;
export const INSTALL_DOCS_URL = `${GITHUB_URL}/blob/main/docs/INSTALL.md`;
export const CLONE_CMD =
  "git clone https://github.com/recepkizilarslan/korkut.git && cd korkut";

// Sıkça sorulan sorular — hem FAQ bölümü hem de FAQPage yapılandırılmış verisi.
export const FAQS = [
  {
    q: "Verilerim nerede tutuluyor?",
    a: "Tüm veriler (sohbetler, belgeler, hesaplar) sizin kendi sunucunuzda kalır. Yerel bir modele bağlandığınızda hiçbir şey makineden çıkmaz. Proje hiçbir telemetri veya analitik göndermez.",
  },
  {
    q: "Korkut açık kaynak mı?",
    a: "Evet. Korkut MIT lisanslı, tamamen açık kaynak bir yazılımdır. Kaynak kodunun tamamı GitHub'da; inceleyebilir, değiştirebilir ve kendi büronuza uyarlayabilirsiniz.",
  },
  {
    q: "Hangi yapay zeka modellerini kullanabilirim?",
    a: "OpenAI uyumlu her modele bağlanabilirsiniz: LM Studio veya Ollama ile yerelde çalıştırın ya da OpenAI, Anthropic, Google Gemini gibi bulut sağlayıcılara bağlanın. Sağlayıcı bağımsızdır, tek satır ayarla değişir.",
  },
  {
    q: "Bulut model kullanırsam verilerim güvende mi?",
    a: "Bulut model seçerseniz giden metin önce en iyi çaba ile rumuzlanır (isim, T.C. kimlik no, dosya no → token). Bu bir azaltıcı önlemdir, garanti değildir. Sınır ötesi veri aktarımı (KVKK md. 9) sorumluluğu size aittir.",
  },
  {
    q: "Kurulum için ne gerekiyor?",
    a: "Tek komutla kurulur; macOS, Linux ve Windows desteklenir. İsterseniz Docker ile yerel Python/Node kurmadan da çalıştırabilirsiniz. Zorunlu TOTP iki adımlı doğrulama ile her kurulum tek büroya hizmet eder.",
  },
  {
    q: "Korkut hukuki tavsiye veriyor mu?",
    a: "Hayır. Korkut hukuki danışmanlık veya temsil sağlamaz. Her çıktı — araştırma, özet, dilekçe/sözleşme taslağı — nitelikli bir avukatın gözden geçirmesi gereken bir taslaktır. Çıktının doğruluğunu teyit etmek kullanıcının sorumluluğundadır.",
  },
] as const;

// Navigasyon bağlantıları
export const NAV_LINKS = [
  { href: "#ozellikler", label: "Özellikler" },
  { href: "#modeller", label: "Modeller" },
  { href: "#kaynaklar", label: "Veri kaynakları" },
  { href: "#kurulum", label: "Kurulum" },
  { href: "#acik-kaynak", label: "Açık kaynak" },
] as const;

// Dokümantasyon sayfalarının tek listesi: sıra, grup, kısa açıklama ve ekran görüntüsü. Kenar menüsü,
// dokümantasyon ana sayfası ve özellik bölümündeki "Dokümantasyonu oku" bağlantıları buradan okur.
// Her kaydın içeriği src/pages/dokumantasyon/<slug>.md dosyasındadır.

export type Doc = {
  slug: string;
  title: string;
  group: string;
  description: string;
  shot?: string;
};

export const DOC_GROUPS = ["Başlangıç", "Hukuki iş", "Büro işleyişi", "Ekip ve güvenlik", "Kişiselleştirme"] as const;

export const DOCS: Doc[] = [
  { slug: "kurulum", group: "Başlangıç", title: "Kurulum ve güncelleme",
    description: "Docker ile kurulum, ilk giriş, ayarlar, yedekleme ve güncelleme." },
  { slug: "yapay-zeka", group: "Başlangıç", title: "Yapay zeka modelleri", shot: "/shots/yapay-zeka.png",
    description: "Yerel (LM Studio, Ollama) ya da bulut modeli bağlama, belge arama modeli, kullanım." },
  { slug: "sohbet", group: "Hukuki iş", title: "Sohbet", shot: "/shots/sohbet.png",
    description: "Dosya sohbetleri ve kişisel sohbetler, asistanın neye erişebildiği, onay akışı." },
  { slug: "arastirma", group: "Hukuki iş", title: "İçtihat ve mevzuat araştırması", shot: "/shots/arastirma.png",
    description: "Yargıtay, Danıştay, UYAP Emsal ve mevzuatta arama, kaynaklar paneli, Kararlarım." },
  { slug: "dilekce-ve-sozlesme", group: "Hukuki iş", title: "Dilekçe ve sözleşme taslakları", shot: "/shots/dilekce.png",
    description: "Sohbetten taslak, editörde düzenleme, sürümler, Word ve UYAP (UDF) çıktısı." },
  { slug: "hesaplama", group: "Hukuki iş", title: "Hesaplama araçları", shot: "/shots/hesaplama.png",
    description: "Tazminat, faiz, harç, süre ve vergi hesapları; sohbetten ya da form ile." },
  { slug: "dosyalar", group: "Büro işleyişi", title: "Dosyalar", shot: "/shots/dosya-detay.png",
    description: "Dosya açma, dosya sayfasının sekmeleri, duruşmalar, süreler ve dosya ekibi." },
  { slug: "muvekkiller", group: "Büro işleyişi", title: "Müvekkiller", shot: "/shots/muvekkil.png",
    description: "Müvekkil kartı, dosyalarla ilişki ve iletişim ile kimlik bilgilerinin korunması." },
  { slug: "ajanda", group: "Büro işleyişi", title: "Takvim, görevler ve hatırlatmalar", shot: "/shots/takvim.png",
    description: "Duruşma, süre, görev ve randevular; takvim görünümleri ve hatırlatmalar." },
  { slug: "belgeler", group: "Büro işleyişi", title: "Belgeler", shot: "/shots/belgeler.png",
    description: "Belge yükleme, güvenlik taraması, OCR, dosyaya bağlama ve belge içinde arama." },
  { slug: "mali", group: "Büro işleyişi", title: "Fatura, masraf ve cari", shot: "/shots/butce.png",
    description: "Fatura ve masraf kaydı, tahsilat, dosya bazında mali görünüm." },
  { slug: "roller-ve-yetkiler", group: "Ekip ve güvenlik", title: "Roller ve yetkiler", shot: "/shots/roller.png",
    description: "Üye daveti, hazır ve özel roller, izinlerin tam listesi, dosya ekibi kuralı." },
  { slug: "erisim-kayitlari", group: "Ekip ve güvenlik", title: "Erişim kayıtları", shot: "/shots/erisim.png",
    description: "Hangi olayların kaydedildiği, kimlerin görebildiği ve nasıl filtreleneceği." },
  { slug: "telegram", group: "Ekip ve güvenlik", title: "Telegram", shot: "/shots/telegram.png",
    description: "Büro botu, hesabı bağlama, özel sohbet komutları ve dosyaya bağlı gruplar." },
  { slug: "kisisellestirme", group: "Kişiselleştirme", title: "Kişilik, hafıza ve stil", shot: "/shots/kisilik.png",
    description: "Asistanın üslubu, büronun kalıcı notları, antet, imza ve belge stili." },
];

export const docHref = (base: string, slug: string) => `${base}dokumantasyon/${slug}/`;

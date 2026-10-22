---
layout: ../../layouts/DocLayout.astro
slug: kurulum
---

Korkut bir bulut hizmeti değil, büronuzun kendi bilgisayarına veya sunucusuna kurduğunuz bir yazılımdır. Bütün bileşenler (uygulama, veritabanı, yapay zeka geçidi, OCR) Docker ile tek komutla ayağa kalkar.

## Gereksinimler

- **Docker** ve **Docker Compose v2** (macOS ve Windows'ta Docker Desktop yeterli).
- En az 8 GB bellek önerilir. Yerel bir yapay zeka modeli de aynı makinede çalışacaksa modelin gerektirdiği bellek ayrıca gerekir.
- İnternet bağlantısı yalnızca kurulum, hukuki araştırma ve (seçerseniz) bulut yapay zeka modeli için gerekir.

## Kurulum

Son sürümün kurulum paketini indirip açın ve kurulum betiğini çalıştırın:

```bash
curl -fsSL https://github.com/bircex/korkut/releases/latest/download/korkut.tar.gz | tar xz
cd korkut
./install.sh
```

Windows'ta [son sürüm](https://github.com/bircex/korkut/releases/latest) sayfasından `korkut.zip` dosyasını indirip açın ve PowerShell ile `.\install.ps1` çalıştırın.

Kurulum betiği şunları yapar:

1. Yapay zeka geçidinin iki gizli anahtarını ve yönetici hesabının ilk parolasını üretip `.env` dosyasına yazar. Betiği yeniden çalıştırmak bu değerleri değiştirmez.
2. O sürümün Docker imajlarını `ghcr.io/bircex` üzerinden indirip her şeyi başlatır.
3. Sonunda giriş adresini ve yönetici parolasını ekrana yazar.

Sürümler takvim tabanlı numaralanır (`YIL.AY.SIRA`, örneğin `2026.10.0`). Her sürümde nelerin değiştiği [değişiklik günlüğünde](https://github.com/bircex/korkut/blob/main/CHANGELOG.md) yazar.

### Kaynak koddan kurulum

Depoyu klonlayıp aynı betiği çalıştırırsanız imajlar indirilmez, kaynak koddan derlenir:

```bash
git clone https://github.com/bircex/korkut.git
cd korkut
./install.sh
```

> **`.env` dosyasının bir kopyasını saklayın.** İçindeki `LLMR_MASTER_KEY`, yapay zeka sağlayıcı anahtarlarını şifreler; bu anahtar olmadan girdiğiniz sağlayıcı anahtarları geri okunamaz.

## İlk giriş

Tarayıcıda <http://localhost:8090> adresini açın:

- **Kullanıcı adı:** `korkut-admin`
- **Parola:** kurulumun sonunda ekrana yazılan parola (ayrıca `.env` içinde `KORKUT_ADMIN_PASSWORD` olarak durur)

Giriş yaptıktan sonra:

1. **Ayarlar → Hesap** bölümünden parolanızı değiştirin; isterseniz iki adımlı doğrulamayı açın.
2. **Ayarlar → Yapay zeka** bölümünden bir model bağlayın. Ayrıntılar: [Yapay zeka modelleri](../yapay-zeka/).
3. **Ayarlar → Üyeler** bölümünden büro çalışanlarını davet edin. Ayrıntılar: [Roller ve yetkiler](../roller-ve-yetkiler/).

Büronun adı varsayılan olarak "Hukuk Bürosu"dur; ilk başlatmadan önce `.env` dosyasına `KORKUT_OFFICE_NAME=...` ekleyerek değiştirebilirsiniz.

## Ağdan erişim

Korkut varsayılan olarak yalnızca kurulduğu makineden (`127.0.0.1`) erişilebilir. Büro ağındaki diğer bilgisayarlardan erişmek için `.env` dosyasına `KORKUT_BIND_ADDRESS=0.0.0.0` ekleyip kurulum betiğini yeniden çalıştırın. Uygulamayı internete açacaksanız önüne TLS sonlandıran bir ters vekil (reverse proxy) koymanız gerekir.

## Ayar değiştirme ve güncelleme

Bir ayarı (port, e-posta sunucusu vb.) değiştirmek için `.env` dosyasına ekleyin (seçeneklerin listesi depodaki `.env.example` dosyasındadır) ve `./install.sh` komutunu yeniden çalıştırın.

Güncellemek için yeni sürümü aynı klasörün üzerine açıp kurulum betiğini yeniden çalıştırın. `.env` dosyası pakette olmadığı için ayarlarınız ve verileriniz korunur:

```bash
curl -fsSL https://github.com/bircex/korkut/releases/latest/download/korkut.tar.gz | tar xz
cd korkut && ./install.sh
```

Kaynak koddan kurduysanız `git pull` ve ardından `./install.sh` yeterlidir. Veritabanı değişiklikleri başlangıçta otomatik uygulanır.

## Yönetici parolasını unuttuysanız

```bash
./install.sh --reset-admin-password
```

Windows'ta `.\install.ps1 -ResetAdminPassword`. Yeni parola `.env` dosyasına yazılır, ekrana basılır ve yöneticinin diğer oturumları kapanır.

## Veriler nerede durur, nasıl yedeklenir

| Docker birimi | İçerik |
|---|---|
| `pgdata` | Veritabanı |
| `objects` | Yüklenen belgeler |
| `secrets` | Uygulamanın ürettiği imza ve iki adımlı doğrulama anahtarları |
| `llmr-data` | Yapay zeka geçidi: sağlayıcılar, modeller, kullanım (anahtarlar şifreli) |

Bu birimler yeniden derleme ve güncellemelerde korunur. Hepsini `.env` dosyasıyla birlikte yedekleyin. `docker compose down` Korkut'u durdurur; `docker compose down -v` ise **bütün verileri siler**.

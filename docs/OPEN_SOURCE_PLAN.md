# Açık kaynak planı

Durum: uygulanıyor (2026-10-10)

| Faz | Durum |
|---|---|
| 0. Yayın öncesi temizlik | Kısmen tamam: rate limit düzeltmesi (#196), GCP ve SaaS dağıtım dosyalarının silinmesi (#201), admin allowlist e-postaları (#205). Kalan: git geçmişi kararı (e-postalar ve Google secret geçmişte duruyor) |
| 1. Monorepo | Tamam: ui/ (#197), website/ (#200), tek container (#199), MinIO yerine volume (#198). Eski repolar arşivlendi, repo adı `korkut` oldu |
| 3. Tek büro | Tamam: SaaS kayıt ve platform admin silindi (#205) |
| 2. llmr | Tamam: `install.sh` ve llmr servisi (#207), ayarlardan sağlayıcı ve model seçimi (#208), bütün model çağrıları llmr üzerinden |
| 4. Kurulum ve ayarlar | `install.sh` (#207), kurulumda üretilen `korkut-admin` hesabı (tarayıcıdan ilk kurulumun #203 yerini aldı), Yapay zeka ayarları (#208), Telegram ayarları. Google girişi tamamen kaldırıldı. Kalan: SMTP ayarları, yeniden indeksleme |
| RAG | Tamam: yüklenen belgeler indekse giriyor (#206) |
| 5. Telegram | Tamam: bot, hesap bağlama, Telegram'dan sohbet (#210), hatırlatmalar (#212), akan yanıtlar ve belge/fotoğraf gönderimi |

Hedef: Korkut tek bir açık kaynak repoda (`bircex/korkut`) toplanır. `docker compose up` ile kurulur, her kurulum tek bir büroya hizmet eder. Bütün model çağrıları [llmr](https://github.com/bircex/llmr) üzerinden geçer. Kurulum bir sihirbazla yapılır, ayarlar sonradan UI'dan değiştirilir. Telegram'dan Korkut'la konuşulabilir.

Her faz ayrı bir PR'dır ve kendi başına çalışır durumda bırakılır.

## Mevcut durum (tarama sonuçları)

| Konu | Bulgu |
|---|---|
| UI | `korkut-ui`: React 19 + Vite, ayrı nginx container'ında çalışıyor, backend imajında değil. `/kayit` (SaaS kayıt) ve `/admin` (platform konsolu) sayfaları içeriyor |
| Admin | `korkut-admin`: sadece UI, sunucu mantığı backend'deki `/admin/api`. Tek büroda işe yarayan kısım model kataloğu ve basit istatistikler |
| Website | `korkut-website`: Astro statik site, `UNLICENSED` |
| Pipelines | `korkut-pipelines`: tamamen GCP (VM, Secret Manager, Artifact Registry). Açık kaynakta karşılığı yok |
| Hostlar | Api, Agent.Host, Knowledge.Host, Gateway, Ocr, KikProxy, Worker, Migrator. Hepsi aynı DB'ye bağlanıyor. Worker bugün hiçbir iş yapmıyor. Knowledge `/ingest`'i repoda çağıran yok |
| LLM | Kayıtlı sadece 2 provider var (Anthropic, DeepSeek). `config/model-routing.yaml` hiç okunmuyor. Genel amaçlı `OpenAiCompatibleChatProviderService` tool çağırma ve streaming destekliyor, olduğu gibi llmr'a yönlendirilebilir. Provider katmanının neredeyse hiç testi yok |
| Çok kiracılık | 37 tabloda `office_id`, 33 tabloda RLS, 14 `SECURITY DEFINER` fonksiyon, `OfficeId` 157 dosyada yaklaşık 1535 kez geçiyor |
| Telegram | `Korkut.Telegram` modülü: büro botu, üye bağlama, long polling ile sohbet |
| Hassas içerik | `AdminAllowlist.cs` içinde 3 kişisel Gmail adresi var (git geçmişinde de), `build.yml` içinde GCP proje bilgileri, kodda `korkutai.com`. Git'e girmiş `.env` ya da anahtar yok |
| Prod | app.korkutai.com ve admin.korkutai.com çok kiracılı yapıya bağlı. Faz 3 sonrası main prod'a deploy edilemez |
| llmr imajı | Sadece `ghcr.io/bircex/llmr:latest` yayında (main'i takip ediyor). README'deki `vX.Y.Z` etiketli imajlar ve `2026.9.0` etiketi GHCR'da yok |
| llmr anahtarı | `LLMR_MASTER_KEY` env olarak zorunlu, dosyadan okuma ya da kendiliğinden üretme yok. Anahtar kaybolursa saklanan bütün provider anahtarları okunamaz |
| SSE parser | `OpenAiCompatibleChatProviderService` `data: {"error": ...}` karelerini yok sayıyor ve `[DONE]` gelmeden kapanan akışı başarılı sayıyor. llmr akış ortasında kopan yanıtı tam olarak bu şekilde bildiriyor; bugünkü kodla yarım kalan bir yanıt tamamlanmış gibi kaydedilir |
| SMTP | `MailSetup` ayarları açılışta bir kez okuyor. UI'dan değiştirilebilmesi için çalışma anında okunması gerekiyor. SMTP yokken parola sıfırlamanın hiçbir yolu yok |
| RAG | `IngestionService`'i çağıran yok: yüklenen belgeler vektör indeksine hiç girmiyor. Embedding ayarı bugün hiçbir şeyi etkilemiyor |

## Hedef yapı

```
korkut/
  src/            .NET (mevcut)
  ui/             korkut-ui (git subtree, geçmişiyle)
  website/        korkut-website (lisansı netleşirse, yoksa dışarıda kalır)
  deploy/         docker-compose.yml, .env.example, Caddy örneği, yedekleme betiği
  docs/
```

Compose servisleri (varsayılan):

| Servis | Not |
|---|---|
| `korkut` | Api + Agent + Knowledge tek süreçte, SPA'yı da kendisi sunar. Gateway kalkar |
| `ocr` | Tesseract'ın native bağımlılıkları yüzünden ayrı kalır |
| `kikproxy` | KİK'in gevşek TLS ayarı diğer çağrılara sızmasın diye ayrı kalır |
| `llmr` | `ghcr.io/bircex/llmr`, digest ile sabitlenir (sürüm etiketi yayınlanana kadar). Sadece iç ağda çalışır, `LLMR_TOKEN` ile korunur |
| `postgres` | pgvector |
| `minio` | Belgeler |
| `migrator` | Tek seferlik çalışır |

PgBouncer ve Grafana LGTM `profiles: [ops]` altına alınır, varsayılan kurulumda çalışmaz. `profiles: [local]` ise Ollama ekler: veri hiçbir buluta çıkmadan, tamamen yerelde çalışan bir kurulum mümkün olur.

### Sırlar ve "tek komutla kurulum"

Compose kendi başına rastgele sır üretemez. Kurulum için şu sırlar gerekiyor: `KORKUT_SECRET`, `KORKUT_TOTP_KEY`, Postgres parolaları, MinIO anahtarı, `LLMR_MASTER_KEY` ve `LLMR_TOKEN`.

- **Önerilen:** tek seferlik bir `secrets-init` servisi. Eksik sırları üretir ve bir volume'a dosya olarak yazar. Diğer servisler bunları `*_FILE` env'leriyle okur (postgres ve minio bunu zaten destekliyor, Korkut'a eklenir). Böylece sadece `docker compose up -d` yeterli olur, `.env` dosyası isteğe bağlı kalır.
- **Engel:** llmr `LLMR_MASTER_KEY_FILE` desteklemiyor. Bunun için llmr'a küçük bir upstream PR gerekiyor. O PR gelene kadar `./install.sh` sırları üretip `.env` dosyasına yazar.
- Kurulum sonunda kullanıcıya `LLMR_MASTER_KEY`'in ve sır volume'unun yedeğini alması açıkça söylenir. Bunlar olmadan yedekten geri dönülemez.

## Faz 0: Yayın öncesi temizlik

Repo public olmadan önce mutlaka yapılması gerekenler:

- `AdminAllowlist.cs` ve testteki kişisel e-postalar kaldırılır (admin yapısı Faz 3'te tamamen gidiyor).
- `build.yml` GCP'den GHCR'a (`ghcr.io/bircex/korkut`) çevrilir. `korkut-pipelines`'a dispatch kaldırılır.
- `korkutai.com` sabitleri mevcut `KORKUT_APP_URL` env'ine bağlanır.
- `WebSetup.cs`: `CF-Connecting-IP` koşulsuz güveniliyor. Self-host'ta bu header ile rate limit atlatılabiliyor. Bu header'a ancak bir ayar açıksa güvenilecek.
- `docs/SAAS_ARCHITECTURE.md`, `docs/PROVISIONING.md` ve `docs/migration/*` arşivlenir ya da silinir.
- Geçmişin tamamı gitleaks ile taranır.
- **Karar gerekiyor:** kişisel e-postalar git geçmişinde de var. Ya `git filter-repo` ile geçmiş yeniden yazılır (bütün commit SHA'ları değişir, açık PR'lar kopar) ya da bu kabul edilir.

## Faz 1: Monorepo

- `korkut-ui` → `ui/` (`git subtree add`, geçmişi korunur). `VITE_API_MODE=python` dalı silinir.
- UI build'i backend imajına girer (çok aşamalı Dockerfile: node build → `wwwroot`), `korkut` servisi `UseStaticFiles` + SPA fallback ile sunar. nginx container'ı kalkar.
- Hostların birleştirilmesi bu fazın ikinci PR'ı olur: Api, Agent ve Knowledge tek host'ta toplanır, Gateway kalkar. Agent'ın Knowledge'a giden HTTP çağrısı (`HttpKnowledgeSearch`, Authorization header'ını iletiyor) süreç içi çağrıya döner. Belge yükleme bu sırada `IngestionService`'e bağlanır; böylece RAG ilk kez gerçekten çalışır.
- `korkut-admin`: taşınmaz. İşe yarayan kısımları Faz 4'te ayarlar sekmesi olarak yeniden yazılır. Commit edilmemiş sayfa bölme işi bu yüzden atılabilir (onayınla).
- `korkut-pipelines`: taşınmaz, arşivlenir. Caddy dosyası `deploy/` altına örnek olarak alınır.
- `korkut-website`: lisansı netleşene kadar dışarıda kalır.
- Bozuk `Makefile` yolları ve eski `.gitignore` satırları düzeltilir.
- Eski repolar GitHub'da arşivlenir (README'lerine yönlendirme eklenir).

## Faz 2: llmr

- `llmr` compose'a eklenir. `LLMR_MASTER_KEY` ve `LLMR_TOKEN` ilk kurulum betiğiyle üretilir.
- Korkut tek bir provider kullanır: `OpenAiCompatibleChatProviderService` → `http://llmr:8080/v1`.
- Model adları llmr route set'leri olur: `korkut-chat`, `korkut-drafting`, `korkut-embed`. Hangi gerçek modele gidecekleri UI'dan seçilir. Specialty'ler (dispatcher, petition vb.) bu adlara eşlenir. Bugün koddaki "hearing yerelde kalsın" kuralı ancak bir route set'i `on_device` işaretlenirse karşılanır; işaretlenmezse kural düşer.
- SSE parser düzeltilir: akış ortasındaki `error` karesi ve `[DONE]` gelmeden kapanan akış hata sayılır. `refusal` yanıtı ayrı ele alınır. `: keep-alive` satırları zaten atlanıyor.
- Zaman aşımları: llmr'da provider başına varsayılan `timeout_secs` 120. Uzun dilekçe taslakları bunu aşabilir, sihirbaz provider'ı daha yüksek bir değerle (örneğin 300) oluşturur. Korkut tarafındaki HttpClient zaman aşımı da buna göre ayarlanır.
- Embedding: `OpenAiCompatibleEmbedder` → `http://llmr:8080/v1/embeddings`, model `korkut-embed`. Embedding modeli değiştirilirse yeniden indeksleme uyarısı gösterilir (`EmbeddingVersion` zaten tutuluyor).
- Silinir: `AnthropicChatProviderService`, DeepSeek kaydı, `ModelRouterService` tabloları, `ModelPricing`, "ai" HttpClient'ındaki retry ve circuit breaker (fallback'i llmr yapıyor, iki katmanlı retry istek çoğaltır), `model-routing.yaml`, provider anahtarı env'leri.
- Kalır: PII maskeleme (`RedactingChatProviderService`, veri llmr'a gitmeden önce maskelenir). `LeakedToolCallParser` da kalır.
- `llm_calls` sadeleşir: feature, kullanıcı, prompt fingerprint, token ve gecikme Korkut'ta kalır. llmr `metadata`'yı kaydetmiyor, bu yüzden kullanıcı ve özellik kırılımı Korkut ledger'ında tutulmak zorunda. Maliyet streaming yanıtta son karedeki `llmr_cost` alanından, diğerlerinde `x-llmr-cost` header'ından okunur. `x-llmr-request-id` ledger'a yazılır; llmr'ın usage kaydıyla eşleştirme bu sayede yapılır. Korkut kendi fiyat tablosunu tutmaz.
- Bütçe: büro bazlı bütçe yerine kurulum geneli isteğe bağlı aylık limit (varsayılan kapalı).
- Backend, llmr `/manage` API'sine sadece owner rolüne açık dar bir proxy sunar. `LLMR_TOKEN` tarayıcıya hiç gitmez.
- OpenAI uyumlu provider ve maskeleme dekoratörü için testler eklenir (şu an hiç yok).

## Faz 3: Tek büro

Önerilen yol, şemayı korumak ama SaaS mantığını sökmek:

- **3a (bu faz):**
  - Kayıt/onay akışı (`OnboardingController`, `OnboardingService`, `/kayit`, demo request, signup ticket) kaldırılır.
  - Platform admin'i kaldırılır: `/admin/api`, `AdminAuthHandler`, `AdminAllowlist`, admin TOTP, `admin_*` tabloları, UI'daki `AdminConsole`.
  - "Büro kapalı" kontrolleri ve çok büroda aynı e-posta mantığı kaldırılır.
  - Davetler kalır (büroya üye eklemek için gerekli).
  - Google girişi tamamen kaldırıldı (kod, UI, `google_sub` kolonu, OIDC ayarları). Giriş yalnızca yerel: kurulumun ürettiği `korkut-admin` kullanıcı adıyla, üyeler e-postayla, hepsi parolayla.
- `admin_*` tabloları yeni bir migration ile düşürülür; mevcut migration'lar yeniden squash edilmez.
- SMTP olmadan parola sıfırlama: owner üyelerin parolasını UI'dan sıfırlayabilir. Owner'ın kendisi için `docker compose exec korkut korkut reset-password <e-posta>` komutu eklenir.
- **3b (ayrı karar):** `office_id` sütunları ve RLS tamamen sökülür. 37 tablo, yaklaşık 1500 referans ve 14 SQL fonksiyonu kapsıyor, regresyon riski yüksek. Kullanıcıya görünen hiçbir fark yaratmıyor: tek büro satırı arka planda kalır, RLS de ek bir savunma katmanı olarak çalışmaya devam eder. Önerim 3a ile yetinip 3b'yi şimdilik yapmamak.

## Faz 4: Kurulum sihirbazı ve ayarlar

İlk kurulum:

- `offices` tablosu boşken uygulama her isteği `/kurulum` sayfasına yönlendirir.
- Sahiplenme saldırısına karşı kurulum adresi bir kerelik token ister. Token container loglarına yazılır, kurulum bitince geçersiz olur.
- Adımlar:
  1. Büro adı ve owner hesabı (e-posta + parola, isteğe bağlı 2FA).
  2. LLM: provider seçimi (Anthropic, OpenAI, Gemini, OpenAI uyumlu ya da Ollama) ve anahtar → llmr `test` → model listesi → sohbet, taslak ve embedding modelleri seçilir → route set'ler yazılır.
  3. E-posta (SMTP): isteğe bağlı, atlanabilir.
  4. Telegram bot token'ı: isteğe bağlı, atlanabilir.
  5. Özet ve bitiş.

Ayarlar (`/ayarlar`, sadece owner):

- **Yapay zeka:** provider'lar, anahtar testi, modeller, fallback sırası, preflight durumu.
- **Kullanım:** llmr usage verisi; güne ve modele göre maliyet.
- **Telegram:** token, bağlı kullanıcılar.
- **E-posta.**
- **Sistem:** sürüm, sağlık durumu, yedekleme bilgisi.

Yapılandırma verisi (SMTP, Telegram token) DB'de `AesGcmSecretEncryptor` ile şifreli tutulur. Env ile verilirse env kazanır. `MailSetup` açılışta bir kez okuyor; e-posta göndericisi ayarı her gönderimde okuyacak şekilde değiştirilir, böylece yeniden başlatma gerekmez.

Embedding adımı sadece Faz 1'de belge indeksleme bağlandıktan sonra anlam kazanır. Embedding modeli sonradan değiştirilirse eski vektörler yeni sorgularla karşılaştırılamaz: ayarlar ekranı yeniden indeksleme başlatır ve bitene kadar uyarı gösterir.

## Faz 5: Telegram

- Her kurulumun kendi botu olur, token'ı BotFather'dan alınır.
- Mesajlar long polling (`getUpdates`) ile alınır: self-host'ta public URL ya da TLS gerekmez. Webhook isteğe bağlıdır.
- Bot `Korkut.Telegram` modülü ve hosted service olarak yazılır, mevcut modül klasör düzenine uyar.
- Hesap bağlama: kullanıcı ayarlardan tek kullanımlık bir kod alır ve bota `/start <kod>` yazar. Bağlanmamış hesaplara bot yanıt vermez.
- Sohbet: mesajlar `SessionChatService`'e gider (aynı araçlar, aynı yetkiler). Her Telegram sohbeti bir Korkut oturumuna eşlenir, `/yeni` komutu yeni oturum açar.
- Yanıtlar streaming yerine düzenlenen mesajlarla akar. Telegram'ın sınırları gözetilir: düzenlemeler saniyede bir ile sınırlanır, 4096 karakteri aşan yanıtlar bölünür.
- Polling tek bir süreçten yapılır. Aynı token'la iki replica çalışırsa Telegram 409 döner; bu durum loglanır ve ayarlar ekranında gösterilir.
- Bildirimler: yaklaşan süreler, duruşmalar ve görev hatırlatmaları. Bugün böyle bir zamanlayıcı yok (Worker hiçbir iş yapmıyor). Hatırlatma işi sıfırdan yazılır; e-posta hatırlatmaları da aynı altyapıyı kullanır.
- Dosya: PDF ya da fotoğraf gönderilirse belge yükleme ve OCR akışına girer.
- Gizlilik: bot mesajları uçtan uca şifreli değildir, müvekkil bilgisi Telegram sunucularından geçer. Bu yüzden entegrasyon varsayılan olarak kapalı gelir. Owner açarken bir KVKK uyarısını onaylar. Her kullanıcı da kendi hesabını ayrıca bağlar.

Durum (ilk PR):
- Owner Ayarlar → Telegram'da bot anahtarını girer. Anahtar `getMe` ile denetlenir ve şifreli saklanır. Botu açmak için KVKK onayı gerekir. Başka bir bota geçilirse bağlantılar silinir.
- Üye Ayarlar → Hesap'ta "Telegram'ı bağla"ya basar. 15 dakika geçerli, tek kullanımlık kod `t.me/<bot>?start=<kod>` bağlantısıyla gider. Kodun yalnızca SHA-256'sı saklanır.
- Mesajlar üyenin rolündeki yetkilerle, normal bir sohbet turu olarak yanıtlanır. HTTP isteği olmadan çalışabilmesi için `ActingUser` eklendi; büro araçları üyeyi web'deki gibi görür. Sohbetler web'deki sohbet listesinde de görünür.
- Yanıt yazıldıkça görünür: ilk kelimeler bir mesaj açar, mesaj en fazla 1,5 saniyede bir düzenlenir, bitince tam yanıtla değiştirilir. Uzun yanıtlar 4096 karakterde paragraf sınırından bölünüp sonraki mesajlarda sürer. İlk kelimeye kadar "yazıyor…" gösterilir.
- Belge ya da fotoğraf gönderilirse (en fazla 20 MB, Bot API sınırı) uygulamadaki yükleme gibi işlenir: metin çıkarılır, taranmış belge ve fotoğraflar OCR'dan geçer, açıklama soru olur. OCR servisi çağıranı kendisi doğruladığı için, HTTP isteği olmayan bu akışta üye adına 15 dakikalık bir erişim token'ı üretilir (`IActingUserCredential`).
- 401/409/erişim sorunları loglanır ve ayarlar ekranında gösterilir. Bot anahtarı URL'de olduğu için bu istemcinin logları kapalıdır, izlerde `/bot` yolları filtrelenir.
- Mesaj konumu (`update_offset`) yanıtlamadan önce kaydedilir: yeniden başlatmada bir mesaj iki kez yanıtlanmaz.
- `KORKUT_TELEGRAM_API_URL` Bot API adresini değiştirir (testler ve kendi Bot API sunucusunu kullananlar için).

Hatırlatmalar (`Korkut.Reminders`): zamanlayıcı dakikada bir çalışır, Telegram'a ve büronun bildirim adresine yazar.
- Süre: son günden `ReminderDaysBefore` gün önce saat 09:00'da (Türkiye saati), son gün bitene kadar. Bağlı bütün üyelere gider.
- Duruşma: bir gün önce saat 09:00'da, duruşma başlayana kadar. Bağlı bütün üyelere gider.
- Ajanda kaydı: başlangıçtan `RemindBeforeMin` dakika önce; "hatırlatma" türünde değer yoksa başlangıçta. Gün boyu kayıtlar 09:00'da başlar sayılır. Atanan kişiye, atanmamışsa herkese gider.
- Her hatırlatma bir kez gider (`sent_reminders`); tarih değişirse yenisi kurulur. Korkut kapalıyken zamanı gelenler, kayıt hâlâ ilerideyse açılınca gider.
- Tekrarlayan ajanda kayıtlarında (`Rrule`) yalnızca ilk tekrar hatırlatılır.
- Kullanılmayan `DeadlineReminderService` (outbox'a yazıp hiç işlenmeyen) silindi.

## Kapsam dışı

- Website (lisans kararından sonra).
- `office_id` ve RLS sökümü (Faz 3b).
- WhatsApp.

## Senin vereceğin kararlar

0. Prod (app.korkutai.com): Faz 3'ten sonra main artık çok kiracılı prod'a deploy edilemez. SaaS kapatılsın mı, yoksa son SaaS sürümü ayrı bir branch'te dondurulsun mu? Prod'da gerçek büro verisi varsa dışa aktarma da planlanmalı.
1. Kişisel e-postalar git geçmişinde: geçmiş yeniden yazılsın mı, yoksa kabul mü edilsin?
2. Faz 3b (RLS ve `office_id` sökümü) yapılsın mı, yoksa ertelensin mi? Önerim ertelemek.
3. `korkut-admin`'deki commit edilmemiş iş atılabilir mi?
4. Website açık kaynağa girsin mi, girerse hangi lisansla?
5. İmajlar GHCR'da `ghcr.io/bircex/korkut` adıyla mı yayınlansın? Repo adı `korkut` olarak değişsin mi?
6. llmr tarafı (BirceX): `LLMR_MASTER_KEY_FILE` desteği eklensin ve sürüm etiketli imajlar (`2026.9.0`) gerçekten yayınlansın mı? İkisi de küçük iş, ama llmr reposunda yapılır.

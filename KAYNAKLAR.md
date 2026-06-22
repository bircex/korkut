# Hukuki Veri Kaynakları — Erişim Listesi

> Korkut'un araştırma ve dilekçe modüllerinin ham maddesi. Türkiye'de **resmî,
> dokümante, SLA'lı açık API neredeyse yok**; erişim çoğunlukla kamuya açık portalların
> arkasındaki JSON uçları (yarı-resmî), scraping veya ticari sağlayıcı API'leri ile.

## 0. Strateji — sıfırdan yazma, hazır katmanı kullan
İki açık kaynak proje işin %90'ını çözüyor; altlarındaki gerçek uçları da belgeliyorlar:
- **yargi-mcp** — tüm yargı kararları + idari kurul kararları. github.com/saidsurucu/yargi-mcp
- **mevzuat-mcp** — mevzuat (kanun/KHK/yönetmelik/Cumhurbaşkanlığı kararnameleri...).
  github.com/saidsurucu/mevzuat-mcp
- Her ikisi de **FastMCP** (Python). Korkut bunları ya MCP olarak tüketir, ya da
  altlarındaki uçları (özellikle **Bedesten API**) doğrudan çağırır.
- **Klonlandı:** `vendor/yargi-mcp`, `vendor/mevzuat-mcp` (MIT). İnceleme + vendor kaynağı.

### Koddan doğrulandı — TOKEN GEREKMİYOR
- Veri erişimi **düz httpx** ile, **auth yok** (`bedesten_client.py`: "Pure httpx - no
  authentication or Playwright needed"). Tarayıcı taklidi header'lar + JSON sarmalı yeter.
- `.env`'deki Clerk/Stripe/JWT/Redis/OpenRouter anahtarları **veri için değil** — adamın
  ücretli SaaS katmanı (giriş/ödeme/oturum) için. Biz `ENABLE_AUTH=false` ile atlıyoruz.
- Tek opsiyonel: mevzuat "semantic" araması → OpenRouter **veya tam yerel embedding**
  (HF TEI / Ollama, dış servis yok). Yerel seçilirse "veri ofiste" ilkesi korunur.

## 1. Yargı kararları (içtihat)

| Kaynak | Ne içerir | Resmî portal | Erişim |
|--------|-----------|--------------|--------|
| **Bedesten API** ⭐ | Yargıtay + Danıştay + yerel mahkeme + istinaf + KYB (5 tür, birleşik) | `bedesten.adalet.gov.tr` | Yarı-resmî JSON, **auth yok** |
| **Yargıtay** | Yargıtay kararları | `karararama.yargitay.gov.tr` | JSON uç (native + Bedesten) |
| **Danıştay** | Danıştay kararları | `karararama.danistay.gov.tr` | JSON uç (keyword + detaylı + Bedesten) |
| **UYAP Emsal** | Yerel + istinaf emsal kararları | `emsal.uyap.gov.tr` | UYAP JSON ucu |
| **Anayasa Mahkemesi** | Norm denetimi + bireysel başvuru | `anayasa.gov.tr` | Resmî arama ucu |
| **Uyuşmazlık Mahkemesi** | Görev/hüküm uyuşmazlıkları | resmî URL'ler | Doğrudan belge URL'leri |

## 2. İdari kurul/kurum kararları

| Kaynak | Ne içerir | Portal | Erişim |
|--------|-----------|--------|--------|
| **KİK** | Kamu İhale Kurulu kararları | `kik.gov.tr` | KİK v2 API, auth yok |
| **Rekabet Kurumu** | Rekabet kararları | `rekabet.gov.tr` | Resmî uç |
| **Sayıştay** | Sayıştay kararları | `sayistay.gov.tr` | Resmî uç |
| **GİB** | Vergi özelgeleri/mevzuat | `gib.gov.tr` | Resmî JSON API |
| **KVKK** | Veri koruma kararları | `kvkk.gov.tr` | Site-içi arama (Brave API key gerekir) |
| **BDDK** | Bankacılık düzenleme | `bddk.org.tr` | Arama + URL (Brave API key) |
| **Sigorta Tahkim** | Sigorta tahkim kararları | `sigortatahkim.org` | Tavily arama + PDF (API key) |

> Not: KVKK/BDDK/Sigorta Tahkim'in resmî ucu yok; üçüncü taraf arama (Brave/Tavily)
> üzerinden gidiliyor → ayrı API anahtarı + daha kırılgan. Düşük öncelik.

## 3. Mevzuat ve kararnameler (mevzuat-mcp ile)

`mevzuat.gov.tr` (Cumhurbaşkanlığı Mevzuat Bilgi Sistemi) üzerinden, tür filtreli:
- **KANUN, KHK, TÜZÜK, YÖNETMELİK, TEBLİĞ, KKY, UY, MÜLGA**
- **Cumhurbaşkanlığı:** CB_KARARNAME, CB_KARAR, CB_YONETMELIK, CB_GENELGE
- Başlık-içi arama, tam cümle, **Resmî Gazete tarih/sayı filtresi**, içeriği Markdown
  olarak çekme. (21 araç mevzuat.gov.tr + 5 araç bedesten.adalet.gov.tr.)
- Anlamsal (semantic) arama opsiyonu var (OPENROUTER_API_KEY gerektirir).

### Resmî Gazete (ek)
- `resmigazete.gov.tr` — günlük yayım (kararnameler, atamalar, ilanlar).
- **Resmî açık API yok** → arşiv scraping (tarih bazlı HTML/PDF). Çoğu kararname zaten
  mevzuat.gov.tr'de yapılandırılmış; Resmî Gazete'yi yalnızca "o günkü ham yayım" için.

## 4. Kendi dosyaların / duruşma günleri (duruşma modülü — AYRI kategori!)
Bunlar içtihat değil; **ofisin kendi davalarının** safahatı ve duruşma günleri.
- **UYAP Avukat Portal** (`avukat.uyap.gov.tr`) / **Vatandaş Portal**
  (`vatandas.uyap.gov.tr`).
- **Resmî açık API YOK.** Giriş **e-Devlet / e-imza / mobil imza** ile; oturum bazlı.
- Sonuç: Duruşma günü otomasyonu **yarı-otomatik** olur (oturum açıp scraping) veya
  **manuel giriş** (planda Faz 3 başlangıcı manuel). Resmî entegrasyon ayrı iş.
- ⚠️ Otomatik erişimde UYAP kullanım şartları + KVKK dikkat.

## 5. Uluslararası (gerekirse)
- **HUDOC (AİHM/ECHR)** — `hudoc.echr.coe.int`. Türkiye kararları için. Genel
  arama/erişim ucu mevcut (doğrulanacak). İnsan hakları dosyalarında değerli.

## 6. Ticari hukuk veritabanları (ücretli, en zengin içerik)
İçtihat + mevzuat + literatür + sözleşme örnekleri entegre. API/entegrasyon genelde
kurumsal anlaşmayla; standart self-serve API yaygın değil — talep edilmeli.
- **Lexpera** (`lexpera.com.tr`) — 1.6M+ Yargıtay kararı, kitap/makale tam metin.
- **Kazancı** — içtihat + mevzuat otomasyonu.
- **HukukTürk**, **Jurix** (dergi/makale).
> Karar: Önce ücretsiz resmî/yarı-resmî kaynaklarla (Bölüm 1-3) git; içerik/kapsam
> yetmezse ticari API'yi değerlendir.

## 7. Risk / dikkat notları
- Bölüm 1-3'teki "API"ler çoğunlukla **dokümante edilmemiş, ters-mühendislikle bulunmuş
  JSON uçları** → habersiz değişebilir, SLA yok. MCP sarmalayıcılar bu bakımı üstleniyor
  ama yine de kırılma riski var.
- **Rate limit:** Bedesten ölçülen ≈ **IP başına 30 sn'de 10 istek** (429 + Retry-After);
  diğer uçlar belgesiz. Kibar ol (token-bucket/backoff), toplu kaba çekimden kaçın. "Hepsini
  indir" gerçekçi değil (milyonlar = yıllar). Strateji: KORPUS.md.
- **Kullanım şartları:** Otomatik erişimde her portalın ToS'unu kontrol et; özellikle
  UYAP (kendi dosyalar) hassas.
- KVKK/BDDK/Sigorta Tahkim üçüncü-taraf aramaya bağlı → düşük öncelik, ek anahtar.

## 8. Entegrasyon kararı (araştırma modülü için)
- **Karar: vendor → tool** — `vendor/`'a klonlandı; MIT olduğu için çekme istemcilerini
  (`bedesten_client.py`, `mevzuat_client.py` vb.) `tools/` altında birer **CLI tool**'a
  uyarlarız: `ictihat_ara`, `mevzuat_ara`, `karar_metin` (her biri CLI + tool.yaml +
  README, MIMARI.md §12). "Adam nasıl alıyorsa öyle" — auth yok, sadece httpx.
- **Lisans yükümlülüğü:** Alınan dosyalarda MIT telif notunu koru.
- **Çekirdek hedef uçlar:** Bedesten API (içtihat + mevzuat), karararama.yargitay/danistay,
  emsal.uyap, anayasa, gib. KVKK/BDDK/Sigorta-Tahkim (3. taraf aramaya bağlı) → sonraya.
- **Alternatif (her an açık):** MCP'leri olduğu gibi Docker'da çalıştırıp Korkut'u MCP
  client yapmak. Vendor yolu kontrolü artırdığı için onu seçtik; bu da yedek.
- **Bakım riski:** Uçlar dokümante değil, tarayıcı-taklidi header'lara dayalı →
  değişebilir. Vendor edince güncellemeyi `vendor/`'daki upstream'den diff'leyerek izleriz.

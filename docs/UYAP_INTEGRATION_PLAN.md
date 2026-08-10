# UYAP Entegrasyon Planı (Tarayıcı Eklentisi)

Tarih: 2026-08-02. Durum: ONAY BEKLİYOR.

## 1. Neden tarayıcı eklentisi?

UYAP'ın avukatlara açık bir API'si yok; Avukat Portal'a e-imza veya mobil imza ile
girilebiliyor ve oturum sunucu tarafında kısa ömürlü. Bu yüzden pazardaki bütün
ürünler (LexPanel, Cübbe, Av. Asistan, İmerek, UYAP Downloader) aynı modeli kullanıyor:
avukatın kendi tarayıcısında, kendi yetkili oturumu içinde çalışan bir Chrome eklentisi
portalın kendi JSON uçlarını çağırır ve veriyi ürünün API'sine basar.

Bizim model de bu: **kullanıcı tetiklemeli, avukatın kendi oturumu, veri Korkut
API'sine kullanıcının kendi eklenti tokenı ile gider.** Robot oturum açma, imza
otomasyonu, oturum çalma yok.

## 2. Bileşenler

### 2.1 `korkut-uyap-extension` (yeni repo)

Chrome Manifest V3 eklentisi. TypeScript + Vite, framework'süz veya Preact.

- **Host permission:** `https://avukat.uyap.gov.tr/*` (ve varsa yeni arayüz alan adları).
- **İçerik:** background service worker + portal sekmesinde çalışan content script +
  eklenti popup'ı (Korkut hesabı bağlama, senkron durumu, "Dosyayı Korkut'a aktar" butonu).
- **Veri çekme:** portalın SPA arayüzünün kullandığı XHR uçları oturum çerezleriyle
  content script üzerinden çağrılır. Uç haritası ilk iş paketinde DevTools ile çıkarılacak
  (dosya listesi, dosya ayrıntısı, taraflar, duruşmalar, safahat, evrak listesi, evrak indirme).
- **Hız sınırı:** UYAP agresif throttling yapıyor; istekler sıralı, araya bekleme konur,
  429/5xx durumunda üstel geri çekilme. Toplu evrak indirme kuyruğa alınır.
- **Korkut'a bağlanma:** kullanıcı Korkut ayarlarından tek kullanımlık eşleştirme kodu
  üretir, eklentiye girer; eklenti bunu uzun ömürlü ama iptal edilebilir bir eklenti
  tokenına (PAT) çevirir. Token office+user kapsamlı, admin panelinden görünür ve iptal edilebilir.

### 2.2 `Korkut.Uyap` modülü (backend, standart modül düzeni)

`Entities/Repositories/Services/Extensions/Controllers` + 3 katman test (Finance referans).

Uçlar (Gateway üzerinden, eklenti tokenı ile):

- `POST /api/uyap/pairing` ve `POST /api/uyap/pairing/exchange`: eşleştirme kodu → PAT.
- `POST /api/uyap/import/files`: dosya + taraf + duruşma + safahat toplu aktarımı.
  Gövde eklentinin normalize ettiği JSON; **idempotent**, dış anahtar
  `(birim_id, dosya_no, dosya_yılı, yargı_türü)`.
- `POST /api/uyap/import/files/{id}/documents`: evrak metadata + içerik (multipart).
  Evrak bazında idempotent (UYAP evrak id'si).
- `GET /api/uyap/sync-state?keys=...`: eklentinin "bunlardan hangisi zaten Korkut'ta,
  son evrak hangisi" diye sorup sadece farkı göndermesi için.

### 2.3 Eşleme (mevcut modüllere dokunuşlar)

Matter/Client/Hearing entity'lerine alan EKLEMİYORUZ; ayrı bir bağ tablosu tutuyoruz:

- `uyap_links` (Korkut.Uyap içinde, RLS'li tenant tablosu): `office_id, local_type
  (matter|document|hearing|client), local_id, uyap_key, last_seen_at, last_synced_snapshot
  (jsonb), deleted_at (tombstone)`. Böylece delta senkron, "zaten aktarılmış" kontrolü ve
  3 yönlü merge (§7.2) tek yerden yapılır.
- Dosya → `Matter` (FileNo = UYAP esas no, CourtNameSnapshot = birim adı, CaseType,
  Counterparty). Mahkeme `Korkut.Courts`'ta yoksa oluşturulur.
- Taraflar → müvekkil tarafı `Korkut.Clients` (varsa isimden eşle, yoksa öner),
  karşı taraf şimdilik `Matter.Counterparty` metni (rehber modeli Part B'de zaten ertelenmişti).
- Duruşma/keşif → `Korkut.Hearings` + Ajanda görünümü.
- Safahat → matter zaman çizelgesi notu (ilk fazda `Notes`, sonra ayrı tablo gerekirse).
- Evrak → `Korkut.Documents` (MinIO) → OCR → `Korkut.Knowledge` ingest → RAG'de
  "büro belgeleri" olarak sohbete girer. PII redaksiyonu mevcut Korkut.Pii hattından geçer.

### 2.4 UDF dönüşümü

UYAP evrakları çoğunlukla UDF (zip içinde content.xml + format bilgisi), bir kısmı PDF/TIF.
Backend'e `UdfConverter` (Korkut.Documents altında servis): UDF → metin + PDF üretimi.
UDF şeması basit; harici bağımlılık gerekmeden parse edilebilir. PDF/TIF mevcut OCR hattına girer.

## 3. Güvenlik ve hukuk

- Eklenti UYAP kimlik bilgisi GÖRMEZ ve SAKLAMAZ; sadece açık oturumun çerezleriyle,
  kullanıcının tetiklediği sorguları yapar. E-imza/oturum açma otomasyonu kapsam dışı.
- Eklenti tokenı: office+user kapsamlı PAT, hash'lenerek saklanır, admin görünür/iptal eder,
  son kullanım damgası tutulur.
- Aktarılan her kayıt audit log'a yazılır (kim, hangi dosya, kaç evrak).
- KVKK: veri zaten avukatın vekâletle erişebildiği veri; Korkut tarafında mevcut
  RLS + PII redaksiyon + tenant izolasyonu aynen geçerli.

## 4. Fazlar

- **Faz 0 (keşif, 1-2 gün):** Portal uç haritası. Gerçek hesapla DevTools'tan dosya
  listesi/detay/evrak isteklerinin URL+şemaları çıkarılır, `docs/UYAP_ENDPOINTS.md`'ye yazılır.
- **Faz 1 (MVP):** Eklenti iskeleti + eşleştirme/PAT akışı + dosya listesi ve seçilen
  dosyaların (taraf+duruşma dahil) tek tık aktarımı. Backend `Korkut.Uyap` modülü,
  `uyap_links`, idempotent import.
- **Faz 2:** Evrak aktarımı: evrak listesi + sıralı indirme kuyruğu + UDF dönüşümü +
  OCR + Knowledge ingest. Dosya detayında "UYAP'tan geldi" rozeti.
- **Faz 3:** Delta senkron (`sync-state` ucu, eklenti açılışta fark basar) +
  duruşmaların Ajanda'ya akışı + safahat zaman çizelgesi + **süre motoru v1**
  (bkz. §6.3: evrak türü + tebligat tarihi → Deadlines'a otomatik süre önerisi).
- **Faz 4 (opsiyonel):** Yeni evrak/tebligat bildirimi (eklenti periyodik fark bulursa
  Korkut bildirimi), toplu ilk kurulum sihirbazı ("tüm aktif dosyalarımı aktar").

## 5. Gruplama modeli

UYAP dosya başına şunu verir: birim, esas no, yargı türü (hukuk/ceza/icra/idari),
dosya türü, taraflar ve rolleri, duruşmalar, safahat, evrak listesi. UYAP "müvekkil kim"
demez ama avukatın vekâletinin hangi tarafa bağlı olduğu bellidir; müvekkil tespiti
buradan otomatik yapılır.

Üç seviyeli model:

1. **Müvekkil (Clients):** vekâlet tarafından otomatik tespit. Aktarım sihirbazında
   UYAP taraf adı ↔ mevcut müvekkil fuzzy eşleştirme; eşleşme yoksa yeni müvekkil önerisi.
   Bir müvekkilin tüm dosyaları mevcut Client→Matter ilişkisi altında toplanır.
2. **Uyuşmazlık kümesi (ilişkili dosya bağı):** aynı ihtilafın birden çok UYAP dosyası
   olur (ilk derece + istinaf + temyiz, asıl + birleşen, dava + ihtiyati haciz + icra
   takibi). Bunlar AYRI Matter kalır, aralarına `uyap_links` üzerinden "ilişkili dosya"
   bağı konur; birleştirme yapılmaz (istinaf dosyası esas dosyayı ezmemeli).
3. **Matter = 1 UYAP dosyası:** birebir eşleme; esas no değişmez dış kimlik.

Segment ayrımı:

- **Dava dosyaları:** az sayıda, yüksek dokunuşlu → varsayılan tam aktarım (evrak dahil).
- **İcra dosyaları:** yüzlerce olabilir, düşük dokunuşlu → varsayılan sadece metadata,
  evrak talep üzerine (on-demand) iner.
- **Kapalı/arşiv dosyalar:** varsayılan dışı, opt-in.

**Evrak taksonomisi:** UYAP evrak türleri ~10 kategoriye indirgenir (dilekçe, karar,
bilirkişi raporu, tebligat, duruşma zaptı, müzekkere, harç/masraf, vekâlet, diğer).
Bu eşleme tablosu üç şeyi birden besler: dosya zaman çizelgesi UI'ı, RAG'de evrak
türüne göre filtreleme, süre motoru tetikleri.

## 6. Kullanım senaryoları (değer sırasıyla)

1. **Sıfır-veri onboarding (en büyük değer):** yeni büronun engeli boş ekran; UYAP
   import ile 15 dakikada tüm portföy (müvekkil + dosya + duruşma) içeride. Bu bir
   özellik değil aktivasyon stratejisi; rakiplerin eklenti yapma sebebi de bu.
2. **AI sohbetin dosya bağlamı kazanması:** evrak → Documents → OCR → Knowledge hattı
   ile "X dosyasındaki bilirkişi raporunu özetle", "cevap dilekçesindeki savunmalara
   karşı beyan hazırla" gerçek olur; drafting'de istinaf yazarken gerekçeli karar eldedir.
   Mevcut agent tool + RAG altyapısı hazır, ek iş sadece ingest etiketleri.
3. **Süre motoru (en güçlü farklılaştırıcı):** evrak türü + tebligat tarihi →
   otomatik süre önerisi (gerekçeli karar tebliği → 2 hafta istinaf, dava dilekçesi
   tebliği → 2 hafta cevap vb.). Yeni evrak düştüğünde Deadlines'a onaylanabilir süre
   kaydı önerilir; süre kaçırma riskini doğrudan adresler. Kural tablosu (evrak türü →
   süre + dayanak madde) ayrı bakımlı bir katalog olur, Faz 3 iş paketi.
4. **Ajanda + durum özeti:** duruşma/keşif otomatik Ajanda'ya; safahattan "bu dosyada
   son ne oldu" AI özeti ve müvekkile durum raporu üretimi.
5. **Portföy analitiği (sonra):** derdest/karar dağılımı, duruşma yükü, Reports'a
   dosya bazlı görünümler.

## 7. Duplication ve override politikası

Senkron eden her üründe en çok patlayan yer burası. Üç ayrı problem var, üçü de tek
bir köken (provenance) modeline oturur: her kayıt `source: uyap | user | ai` taşır ve
üç değişmez kural geçerlidir:

1. **Senkron her zaman upsert'tir:** dış anahtar yoksa kayıt yaratılmaz, körlemesine
   insert yok.
2. **Kullanıcı düzenlemesi makine yazımını her zaman yener:** senkron sadece
   "kullanıcının dokunmadığı" alanları günceller (3 yönlü karşılaştırma).
3. **AI doğrudan yazmaz, önerir:** kullanıcı onaylayana kadar pending; verilen karar
   (kabul VEYA ret) hatırlanır ve aynı öneri bir daha üretilmez.

### 7.1 Import duplication (UYAP → Korkut)

- **Dosya:** `uyap_links` dış anahtarı office kapsamlıdır; aynı büroda iki avukat aynı
  dosyayı senkronlarsa ikinci aktarım upsert olur, ikinci Matter oluşmaz. Farklı
  bürolar (tenant) izole kalır; tenant'lar arası dedup YAPILMAZ (gizlilik gereği).
- **Evrak:** birincil anahtar UYAP evrak id'si; ikincil koruma içerik hash'i (SHA-256).
  Kullanıcının senkrondan ÖNCE elle yüklediği aynı belge (ör. gerekçeli karar PDF'i)
  hash ile yakalanır: aynı matter'da aynı hash varsa yeni kayıt açılmaz, mevcut belge
  UYAP evrakına bağlanır. UDF + PDF aynı evrakın iki sunumuysa tek mantıksal evrak,
  iki rendition olarak saklanır.
- **Tombstone:** kullanıcı senkronlanmış bir belgeyi/kaydı Korkut'tan silerse
  `uyap_links` satırı `deleted_at` ile kalır; sonraki senkron o kaydı YENİDEN GETİRMEZ.
  Geri isteme "silinen UYAP kayıtlarını geri yükle" ile açıkça yapılır.
- **Knowledge ingest:** doküman id + içerik hash ile idempotent; yeniden ingest aynı
  dokümanın chunk'larını siler ve yeniden yazar (delete-then-insert), asla üstüne eklemez.
  Aksi halde RAG aynı içeriği iki kez kaynak gösterir.

### 7.2 Alan bazlı override (senkron vs kullanıcı düzenlemesi)

`uyap_links.payload_hash` yerine link satırında son senkronlanan UYAP anlık görüntüsü
(jsonb snapshot) tutulur. Delta senkronda her UYAP kaynaklı alan için 3 yönlü kural:

- mevcut değer == önceki UYAP değeri → yeni UYAP değeri yazılır (kullanıcı dokunmamış);
- mevcut değer != önceki UYAP değeri → kullanıcı değiştirmiş, DOKUNULMAZ; fark
  `drift` olarak işaretlenir ve dosya detayında "UYAP'ta farklı görünüyor" rozetiyle gösterilir.

Alan sahipliği baştan bellidir: esas no, birim, taraf listesi, duruşma tarihleri,
safahat, evrak listesi UYAP sahipli; başlık (kullanıcı yeniden adlandırdıysa), notlar,
etiketler, müvekkil bağı, Korkut'a özgü alanlar kullanıcı sahipli ve senkron bunlara
hiç yazmaz. Dosya durumu ara durumdur: UYAP'ta dosya kapanınca otomatik kapatma
YAPILMAZ, "UYAP'ta kapandı, kapatalım mı?" önerisi düşer.

### 7.3 AI çıkarımları (süre, taraf, özet)

Dokümanlardan süre/dava bilgisi çıkarımı ayrı bir öneri tablosuna yazılır
(`ai_extractions`: office_id, matter_id, kaynak evrak id, tür, çıkarım anahtarı,
payload, status: pending/accepted/rejected, karar veren + zaman, analysis_version):

- **Dedup anahtarı** `(matter, kaynak evrak, kural/çıkarım türü)`: aynı evrak yeniden
  analiz edilirse var olan öneri (kabul VEYA ret edilmiş) yeniden üretilmez. Ret edilen
  öneri "hatırlanan ret"tir; model her senkronda aynı öneriyi diriltirse kullanıcı
  güveni biter.
- **Kabul edilen öneri** normal kayda dönüşür (ör. Deadlines satırı) ve `source: ai,
  approved_by: user` kökeniyle işaretlenir; kullanıcı sonradan düzenlerse §7.2 kuralı
  gereği yeniden analiz ona dokunamaz.
- **Yeniden analiz / model iyileşmesi:** `analysis_version` ile yeni sürüm YENİ türde
  öneriler ekleyebilir ama karar verilmiş hiçbir öneriye dokunamaz. "Tümünü yeniden
  analiz et" açık bir kullanıcı eylemidir ve yalnızca pending'leri tazeler.
- Süre önerileri hiçbir zaman doğrudan takvim/deadline kaydı OLMAZ; onay akışı
  Chat UX'teki taslak Kabul/Geri Al kalıbının aynısıdır.

### 7.4 Müvekkil dedup

Güçlü anahtar varsa (TCKN/VKN, portal verdiği ölçüde) otomatik bağlama; yoksa
normalize edilmiş ad + fuzzy skor sadece ÖNERİ üretir, sihirbazda kullanıcı onaylar.
Otomatik müvekkil birleştirme asla yapılmaz; yanlış birleştirme yanlış dosyanın
yanlış müvekkile görünmesi demektir, geri dönüşü ağırdır.

## 8. Açık kararlar

1. Eklenti repo'su: workspace'e `korkut-uyap-extension` olarak mı, `korkut-ui` içine mi?
   (Öneri: ayrı repo; yayın döngüsü Chrome Web Store'a bağlı, UI'dan bağımsız.)
2. Firefox/Edge desteği ilk sürümde var mı? (Öneri: sadece Chrome/Chromium; Edge zaten çalışır.)
3. Safahat için ayrı tablo mu, Matter.Notes mu? (Öneri: MVP'de Notes, Faz 3'te tablo.)
4. Uyuşmazlık kümesi bağı MVP'de mi Faz 3'te mi? (Öneri: veri modeli Faz 1'de hazır
   olsun, UI Faz 3'te; sonradan eklemek migration ister.)
5. İcra segmenti MVP kapsamına girer mi? (Öneri: metadata-only olarak evet; icra ağırlıklı
   bürolar için onboarding değeri büyük, maliyeti düşük.)

# Hukuk Bilgi Bankası (Legal KB) Planı

> Durum: ARAŞTIRMA TAMAMLANDI, plan onay bekliyor. Araştırma tarihi: 2026-08-02.
> Amaç: Korkut'un canlı fetch ile çalışan hukuk kaynaklarını (Korkut.Tools/Law) kalıcı bir
> vektör bilgi bankasına taşımak; semantik arama + güncel tutma pipeline'ı kurmak.

## 0. Yönetici özeti

- Türkiye'de mevzuat/içtihat için **resmî bulk veri veya açık API yok**; ekosistem tamamen
  topluluk scraper'larına dayanıyor. Ticari sağlayıcılar (Kazancı, Lexpera, Legalbank...) veri
  lisanslamayı halka açık satmıyor, ancak birebir kurumsal anlaşmayla mümkün.
- Buna rağmen iş yapılabilir çünkü: (1) Bedesten API'si (bedesten.adalet.gov.tr) auth'suz,
  tarih filtreli ve sayfalı olarak mevzuat + 5 içtihat türünü tek şemadan veriyor;
  (2) HuggingFace'te 700 bin kararlık ham korpus ve 1,8 milyon chunk'lık, **BGE-M3
  embedding'leri hazır**, CC-BY-4.0 lisanslı bir set var; (3) MIT lisanslı yargi-mcp /
  mevzuat-mcp projeleri tüm endpoint'lerin referans implementasyonu.
- Strateji: **hazır set ile bootstrap + kendi artımlı crawl'ımız ile güncel tutma.**
  Mevzuat ve regülatör korpusları küçük (toplam ~100 bin belge), tamamı birkaç günde çekilir.
  İçtihat devasa (~11-12 milyon belge); tam arşiv ayrı bir karar, bootstrap + delta ile başlanır.

## 1. Veri envanteri

### 1a. Hazır datasetler (bootstrap için)

| Dataset | İçerik | Boyut | Lisans | Güncellik |
|---|---|---|---|---|
| [muhamparlak/turkish-law-bge-m3-embeddings](https://huggingface.co/datasets/muhamparlak/turkish-law-bge-m3-embeddings) | 1,82M emsal karar chunk'ı (3000 kr + 500 overlap) + 4,7k mevzuat satırı, **ham metin + BGE-M3 1024-dim embedding birlikte** | 4,86 GB parquet | **CC-BY-4.0** | snapshot, yeni karar gelmez |
| [erdem-erdem/Turkish-Law-Documents-700k-clustered](https://huggingface.co/datasets/erdem-erdem/Turkish-Law-Documents-700k-clustered) | 702.295 Yargıtay+Danıştay kararı, ham metin, kümelenmiş | 1,75 GB parquet | belirsiz | snapshot |
| [muhammetakkurt/mevzuat-gov-dataset](https://huggingface.co/datasets/muhammetakkurt/mevzuat-gov-dataset) | 907 kanun, madde madde yapılandırılmış | 37 MB | MIT | Tem 2024'te donmuş |
| [OrionCAF/turkish_law_qa_dataset](https://huggingface.co/datasets/OrionCAF/turkish_law_qa_dataset) | 18,3k hukuk QA çifti | 2 MB | Apache 2.0 | eval seti olarak kullan |

Karar: içtihat bootstrap'inde birincil kaynak **muhamparlak** seti (lisansı en temiz, embedding'i
hazır); erdem-erdem seti lisans belirsizliği yüzünden yalnızca doğrulama/karşılaştırma için.
Mevzuatı hazır setten DEĞİL, doğrudan Bedesten'den kendimiz çekeceğiz (zaten küçük ve set eski).

### 1b. Canlı kaynaklar (crawl hedefleri, hacimler 2026-08-02'de canlı doğrulandı)

| Kaynak | Erişim | Hacim | Update tespiti | Zorluk |
|---|---|---|---|---|
| Mevzuat (Bedesten `/mevzuat/*`) | JSON POST, auth yok, sadece `AdaletApplicationName: UyapMevzuat` header | 20.776 | **mükemmel**: `kayitTarihi` + `guncellemeTarihi` alanları, RG tarihine desc sıralama | 1 |
| Yargıtay (karararama + Bedesten `YARGITAYKARARI`) | JSON POST | **9.942.038** | zayıf: eklenme tarihi yok, kayan pencere + documentId-diff gerekir | 2 |
| Danıştay (karararama.danistay + Bedesten `DANISTAYKARAR`) | JSON POST | 404.487 | aynı strateji | 2 |
| UYAP Emsal (BAM+yerel; Bedesten `YERELHUKUK`/`ISTINAFHUKUK`/`KYB`) | JSON POST | 831.746 | aynı strateji; 429 gözlenmiş, 1 istek/3,5 sn şart | 2 |
| AYM (BB + Norm) | JSON POST `api/core/public/search` | 17.438 + 5.579 | `yayinTarihi` alanı var; haftalık full listeleme + id-diff | 1 |
| Resmî Gazete | server-rendered HTML, `fihrist?tarih=YYYY-MM-DD`, deterministik `/eskiler/...` URL'leri | arşiv 1921'e kadar | günlük cron ile fihrist; RSS/sitemap yok | 1-2 |
| GİB özelgeler | JSON POST `api/gibportal/mevzuat/ozelge/list`, tarihe desc sıralı | 18.356 | trivial incremental | 1 |
| Rekabet Kurumu | HTML scrape, `YayinlanmaTarihi` query filtresi | on binler | yayınlanma tarihi filtresiyle poll | 2-3 |
| KVKK | HTML, listeleme zayıf | yüzler | haftalık scrape + URL-diff | 3 |
| BDDK | HTML liste + PDF | düşük | haftalık id-diff | 3 |
| Sayıştay | DataTables POST + antiforgery token, **WAF var (418 döner)** | on binler | tarih filtreli, yavaş ve session'lı crawl | 3 |
| Sigorta Tahkim | karar DB yok; 3 ayda bir "Hakem Karar Dergisi" PDF (64+ sayı) | seçilmiş kararlar | yeni dergi sayısını izle, PDF'i karar bazında böl | 3 |
| Uyuşmazlık M. | ASP.NET WebForms, VIEWSTATE postback | düşük | kırılgan; düşük frekans | 3-4 |
| KİK/EKAP | JSON ama AES şifreli custom header'lar zorunlu | ? | **ERTELE**: reverse-engineered kripto, sürümle kırılır; canlı tool olarak kalsın | 4-5 |

Referans implementasyonlar (MIT): [yargi-mcp](https://github.com/saidsurucu/yargi-mcp),
[mevzuat-mcp](https://github.com/saidsurucu/mevzuat-mcp),
[mevzuat-gov-scraper](https://github.com/muhammetakkurtt/mevzuat-gov-scraper).
Korkut'un mevcut Law client'ları zaten yargi-mcp'nin 1:1 .NET portu; crawl için yeniden
kullanılacaklar (`ISourceClient.SearchPagedAsync` tarih filtreli sayfalama + `FetchAsync` tam metin).

## 2. Mimari

### 2a. Şema: global korpus, tenant DEĞİL

Mevcut `KnowledgeChunkEntity` OfficeId + RLS'e bağlı (büro belgeleri için doğru). Hukuk korpusu
tüm tenant'lara ortak, o yüzden Knowledge DB'ye RLS'siz yeni tablolar:

- `legal_documents`: source (mevzuat/yargitay/...), external_id (documentId), title, court/daire,
  esas/karar no, karar_tarihi, rg_tarihi, url, content_hash, fetched_at, updated_at.
  Unique (source, external_id). content_hash değişince re-ingest.
- `legal_chunks`: legal_document_id, ordinal, text, embedding (pgvector), embedding_version.
  HNSW index; büyüyünce halfvec + partitioning.
- `crawl_state`: source, cursor (tarih/pageNumber), last_run_at, last_error. Her job kaldığı
  yerden devam eder.

`KnowledgeController`'a corpus parametresi: `scope=office` (mevcut RLS'li arama) ve
`scope=legal` (global korpus). Agent tarafında `KnowledgeSearchTool` iki scope'u da arar,
sonuçları kaynak etiketiyle birleştirir; mevcut canlı Law tool'ları fallback + "en güncel veri"
yolu olarak AYNEN kalır.

### 2b. Embedder: self-host BGE-M3 (TEI üzerinden)

Karar gerekçesi: (1) muhamparlak setinin 1,8M chunk'ının embedding'i zaten BGE-M3, aynı modeli
seçersek bootstrap embedding maliyeti SIFIR; (2) TEI (text-embeddings-inference) OpenAI-uyumlu
`/v1/embeddings` sunar, mevcut `OpenAiCompatibleEmbedder` + `IEmbedder` hiç değişmez, sadece
base URL; (3) Türkçe'de BGE-M3 güçlü ve 1024-dim ile depolama makul; (4) veri dışarı çıkmaz
(PII açısından da temiz, gerçi Korkut.Pii maskesi zaten var).
Compose'a `tei` servisi eklenir (CPU ile başlar, delta hacmi küçük; backfill dönemi için geçici GPU).
`EmbeddingVersion` alanı zaten var, ileride model değişirse re-index izlenebilir.

### 2c. Crawl worker

Yeni host değil, **Knowledge Host içinde hosted background job'lar** (mevcut Jobs altyapısı
neyse ona uyulur): kaynak başına bir job, `crawl_state` cursor'ı ile artımlı çalışır.
Kurallar:
- Host başına politeness: 1 istek / 3,5-4 sn (Bedesten ölçülmüş limiti ~10 istek/30 sn),
  429'da `Retry-After` + exponential backoff, gece saatlerine yoğunlaştır.
- Farklı host'lar (bedesten / karararama.yargitay / karararama.danistay / gib / aym...)
  paralel çalışabilir, aynı host içinde tek sıralı kuyruk.
- Sayıştay ve Uyuşmazlık düşük frekanslı (haftalık) ayrı job; KİK crawl edilmez.
- Her fetch: HTML/PDF → Markdown (mevcut `LegalText`), content_hash karşılaştır, değiştiyse
  chunk + embed + upsert.

### 2d. Lazy ingest (bedava üçüncü kanal)

`FetchDocumentTool` bir kararı her çektiğinde metni `legal_documents`'a da yazar. Büroların
gerçekten kullandığı kararlardan organik bir alt korpus birikir; tam arşiv olmadan bile en
değerli kararlar KB'de toplanır.

## 3. Güncelleme stratejisi (kaynak bazında)

| Kaynak | Kadans | Yöntem |
|---|---|---|
| Resmî Gazete | **günlük** (RG 7 gün yayımlanır) | fihrist çek → yeni kanun/yönetmelik/değişiklik tespit → tetikleyici olarak kullan |
| Mevzuat | günlük (RG tetiklemesiyle) + haftalık tam tarama | Bedesten `guncellemeTarihi`/RG tarihi desc; değişen mevzuatın konsolide metnini yeniden çek, hash farklıysa re-ingest |
| İçtihat (Yargıtay/Danıştay/Emsal) | günlük delta + **kayan pencere** | kararlar aylar sonra yüklendiği için son 24 ayı kararTarihi dilimleriyle periyodik yeniden tara, documentId setine göre yeni olanları al. Yıllık ~300-500k yeni karar ≈ günde 1-2 saat fetch |
| AYM | haftalık | full listeleme (23k kayıt, ~1 saat) + id-diff |
| GİB özelge | haftalık | tarihe desc sıralı liste, cursor'a kadar çek |
| Rekabet | haftalık | `YayinlanmaTarihi` filtresi + PDF çıkarımı |
| KVKK, BDDK | haftalık | liste scrape + URL/id-diff |
| Sayıştay | haftalık, yavaş | token'lı DataTables, WAF'a takılmamak için düşük hız |
| Sigorta Tahkim | aylık | dergi sayfasında yeni sayı kontrolü, yeni PDF'i böl+ingest |
| Uyuşmazlık | aylık | WebForms postback crawl |

İzleme: her job `crawl_state.last_error` + metrik (işlenen belge, hata oranı) yazar;
Grafana'da mevcut LGTM stack'ine pano. Üst üste N başarısız run'da admin'e mail
(mevcut system-SMTP altyapısı).

## 4. Fazlar

**Faz 0, altyapı (2-3 gün iş):** legal_documents/legal_chunks/crawl_state şeması + migration,
TEI servisi compose'a, KnowledgeController scope=legal, KnowledgeSearchTool çift scope.

**Faz 1, mevzuat (1 hafta):** Bedesten mevzuat full crawl: 20.776 belge × ~4 sn ≈ **24 saat**
tek kuyruk. Madde ağacı + gerekçeler dahil. RG günlük cron + haftalık tam tarama jobs.
Embedding: ~0,5-1B token, TEI/CPU ile birkaç gün veya küçük GPU ile saatler. Disk ~5-10 GB.

**Faz 2, regülatörler + AYM + GİB (1-2 hafta):** AYM 23k (~26 saat), GİB 18k (~20 saat),
Rekabet/KVKK/BDDK/Sayıştay/Sigorta Tahkim scrape'leri. KİK ve Uyuşmazlık canlı tool olarak kalır.
Disk ~10-20 GB ek.

**Faz 3, içtihat bootstrap (1 hafta):** muhamparlak parquet'ini indir, 1,8M chunk'ı embedding'iyle
birlikte `legal_chunks`'a bulk COPY (embedding maliyeti $0). Kaynak URL/documentId metadata'sını
`legal_documents`'a eşle. Lazy ingest'i aç. Disk ~15-25 GB.

**Faz 4, içtihat delta (sürekli):** günlük Bedesten delta + 24 aylık kayan pencere jobs.
Bu noktada sistem "bugünden ileriye tam, geçmişe doğru 700k+lazy" kapsamındadır.

**Faz 5, OPSİYONEL tam arşiv (ayrı karar):** kalan ~10M kararın backfill'i. Politeness
limitleriyle gerçekçi süre: listeleme ~40 gün, tam metin fetch tek kuyrukta ~14 ay,
3 host'a paralel bölününce **6-12 ay**. Embedding ~20-25B token: OpenAI small ile ~$400-500
veya kiralık GPU ile ~2.000-2.500 GPU-saat (~$700-1.200). Disk 150-250 GB + HNSW için bol
RAM'li ayrı DB düğümü (aylık ~$100-250). Ancak "içtihatta semantik arama" başlı başına ürün
özelliği olacaksa girilir; Faz 3+4 çoğu ihtiyacı karşılar.

## 5. Maliyet özeti

| Kalem | Faz 1-4 | Faz 5 (tam arşiv) |
|---|---|---|
| Embedding | ~$0 (hazır embedding + TEI self-host) | ~$400-1.200 |
| Disk | ~30-50 GB (mevcut VPS'e sığar) | +150-250 GB |
| Sunucu | TEI için CPU yeterli, ~$0 ek | bol RAM'li DB düğümü ~$100-250/ay |
| Geliştirme | ~3-4 hafta iş | +crawl'ın aylarca babysitting'i |
| Crawl süresi | toplam ~1 hafta makine zamanı | 6-12 ay |

## 6. Riskler ve notlar

- **Hukuki/lisans:** hiçbir resmî sitede açık kullanım koşulu veya API sözleşmesi yok; robots.txt
  engellemiyor ama resmî izin de yok. Karar metinleri kamu belgesi ve anonimleştirilmiş
  yayımlanıyor. Hazır setlerden CC-BY-4.0 (muhamparlak) ve MIT (muhammetakkurt) ticari kullanım
  için temiz; lisanssız fikriokan/erdem-erdem setlerine yaslanma. Ürün içinde her sonuçta kaynak
  URL göster (zaten SourceResult.Url var).
- **API kırılganlığı:** Bedesten dokümante değil, sessizce değişebilir; crawl job'ları şema
  değişikliğinde alarm üretmeli. KİK kriptolu, Uyuşmazlık VIEWSTATE'li: bunlar KB dışı.
- **Veri kalitesi:** karararama'da bozuk tarihler var ("21.09.6006"); ingest'te tarih doğrulama.
- **Güncellik yanılgısı:** içtihat kaynaklarında "sisteme eklenme tarihi" yok; kayan pencere
  olmadan delta kaçırılır. Mevzuatta yürürlükten kalkanları da işaretlemek gerekir
  (RG fihristi + Bedesten güncelleme alanı bunun için).
- **Cevap katmanı:** KB araması "en güncel" garantisi vermez; assistant cevaplarında KB hit'i +
  gerektiğinde canlı tool doğrulaması birlikte kullanılmalı (özellikle mevzuat sorularında
  yürürlük kontrolü).

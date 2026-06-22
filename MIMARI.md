# Mimari — Teknik Tasarım

> PLAN.md yol haritası; bu doküman *nasıl çalışır*ın detayı. Kuruluma başlamadan önce
> netleştirilecek tüm yapısal kararlar burada.

## 1. Süreç topolojisi (tek Linux sunucu)

```
┌─ Docker ─────────────────────────────┐
│  mattermost      (web + WebSocket)    │
│  mattermost-db   (Postgres, sadece MM)│
└───────────────────────────────────────┘
        ▲ WebSocket (al) + REST (yaz)
        │
┌─ systemd servisi ────────────────────┐
│  asistan  (tek Python süreci)         │
│    ├─ bridge/      MM dinle (kanal+DM)  │
│    ├─ korkut/      tek kimlik; içeride uzmanlık modülleri (paralel)
│    └─ core/        router+redactor+db  │
└───────────────────────────────────────┘
        │
┌─ Docker ─────────────────────────────┐
│  asistan-db   (Postgres, uygulama)    │
└───────────────────────────────────────┘
        ▲
┌─ cron ───────────────────────────────┐
│  hatirlatma  (her sabah core'u çağırır│
│              → MM'e duruşma özeti yazar)│
└───────────────────────────────────────┘
```

- **3 çalışan parça:** Mattermost (Docker), `asistan` (Python servisi), `cron` (hatırlatma).
- Mattermost kendi Postgres'ini kullanır; **uygulama verisi ayrı Postgres** database'inde
  (aynı sunucu, farklı DB). Baştan Postgres → çok kullanıcı/ofise büyümeye hazır.
- Hepsi aynı makinede; dış bağımlılık yalnızca OpenAI API (rumuzlanmış metinle).

## 2. Mattermost bot kimliği — tek "Korkut"
- **Tek bot hesabı: Korkut** (tek token). Kullanıcı tek bir asistanla konuşur.
- İçeride uzmanlık modülleri (yönlendirme, duruşma, dilekçe, araştırma) var; gerekirse
  **paralel** çalışır ama **hepsi Korkut olarak** yazar → kullanıcıya tek kimlik görünür.
- `asistan` servisi tek WebSocket ile dinler, REST ile **Korkut** olarak yazar.
- **İki yüzey:** (a) kanal — `@Korkut` etiketi / Korkut'un thread'i; (b) **özel mesaj (DM)**
  — Korkut'la birebir yazışma.
- **Döngü koruması:** Servis, Korkut'un kendi mesajlarını yok sayar.

## 3. Mesaj akışı (adım adım)

```
1. Kullanıcı kanala veya Korkut'a DM yazar.
2. bridge mesajı WS'ten alır. Korkut'un kendi mesajıysa → yok say (dur).
3. Tetikleme:
     a) DM ise → her mesaj Korkut'a gider (özel sohbet; etiket gerekmez).
     b) Kanalda @Korkut etiketli VEYA Korkut'un thread'ine yanıtsa → Korkut'a gider.
     c) Kanalda etiketsiz → yok say (kanalı boğmaz, maliyet yok).
4. Korkut'un yönlendiricisi niyeti anlar → ilgili uzmanlık modülü(ne) yönlendirir
   (duruşma / dilekçe / araştırma); birden çoğu gerekiyorsa PARALEL çalışır.
5. Modül DB'den veriyi çeker; LLM gerekiyorsa core.router'a (uzmanlık, is_tipi, metin) verir.
6. core.router: uzmanlık→model eşlemesi.
     - model=yerel & Ollama kapalı → bulut'a düş + logla.
     - model=bulut → redactor.rumuzla → provider(gpt) → redactor.geri_ac.
7. Çıktılar **tek Korkut yanıtında** birleşir; thread'e/DM'e **Korkut kimliğiyle** yazılır.
8. Veri yazımı varsa (duruşma/dilekçe) → DB/dosya + onay durumu.
```

- **Bağlam = Mattermost thread'i / DM dizisi.** Korkut o dizinin mesajlarını bağlam
  olarak görür. Kalıcı bilgi (dava/müvekkil) DB'de.

## 4. core sözleşmeleri (arayüzler)

```
router.cevapla(uzmanlik: str, is_tipi: str, mesajlar: list) -> str
    # uzmanlık→model seç, yerel/bulut, düşme kuralı; redaction'ı kendi içinde uygular.

redactor.rumuzla(metin: str) -> (rumuzlu_metin: str, harita: dict)
redactor.geri_ac(metin: str, harita: dict) -> str

providers.gpt.uret(mesajlar: list, ayar: dict) -> str      # OpenAI
providers.ollama.uret(...)                                  # sonra
providers.claude.uret(...)                                  # sonra

db: müvekkil / dava / duruşma / dilekçe CRUD (aşağıdaki şema)

tool_registry.yukle() -> list[Tool]          # tools/ tarayıp manifestleri okur
tool_registry.calistir(ad: str, args: dict) -> dict   # bir tool'u çalıştır, JSON döndür
    # Tool'lar tools/ altında; CLI'dan da çağrılabilir. Detay: §12.
```

- **Sağlayıcı eklemek = `providers/` altına bir dosya + config'e bir satır.** Ajanlar
  ve bridge hiç değişmez.
- **Yeni yetenek eklemek = `tools/` altına bir tool** (CLI + tool.yaml + README). §12.

## 5. Rumuzlama akışı (bulut'a giden her metin)

```
metin ──> tespit (regex + isim sözlüğü)
            • TCKN (11 hane + algoritma)   • telefon   • IBAN
            • plaka                         • esas/dosya no
            • kişi adı (sözlük + kural)
       ──> token ile değiştir:  "Ahmet Yılmaz" → "[KİŞİ_1]"
       ──> harita: { "[KİŞİ_1]": "Ahmet Yılmaz", ... }  (RAM'de, isteğe özel)
       ──> provider(gpt) çağrısı  (yalnızca rumuzlu metin gider)
       ──> dönen cevapta token'ları geri koy
```

- `yerel` modele giden metin **rumuzlanmaz** (zaten dışarı çıkmıyor).
- Harita istek başına RAM'de; kalıcı tutulursa (audit) `data/`'da şifreli. [KARAR-3]

## 6. Veri modeli (Postgres — ilk taslak)

```sql
müvekkil(id, ad_soyad, tckn, telefon, eposta, adres, notlar, olusturma)
dava(id, dosya_no, müvekkil_id, mahkeme, dava_turu, karsi_taraf,
     durum, acilis_tarihi, notlar)
duruşma(id, dava_id, tarih, saat, yer, tur, sonuc,
        hatirlatma_durumu, notlar)
dilekçe(id, dava_id, tur, dosya_yolu,
        durum,            -- taslak | onay_bekliyor | onaylandi
        olusturan, olusturma)
```

- Tüm hassas alanlar zaten LUKS şifreli diskte. TCKN için alan-bazlı ek şifreleme
  opsiyon (gerekirse). `ofis` alanı çok-ofis fazında eklenecek.

## 7. Korkut'un iç yapısı (tek kimlik, uzmanlık modülleri)
Kullanıcıya tek **Korkut** görünür. İçeride yönlendirici niyeti anlar, ilgili modül(ler)i
çalıştırır (paralel olabilir), çıktılar **tek Korkut yanıtında** birleşir.

| Modül (içeride) | İş | LLM / araçlar | Veri |
|-----------------|----|---------------|------|
| **yönlendirici** | niyet anla → modüle ver, genel sohbet | sınıflama | — |
| **duruşma** | duruşma ekle/listele, hatırlatma | tarih/varlık çıkarımı | duruşma, dava |
| **dilekçe** | şablon seç + taslak üret → .docx | metin üretimi | dava, dilekçe, şablon |
| **araştırma** | mevzuat/içtihat ara + özetle | tools/ (ictihat_ara, mevzuat_ara, karar_metin) | dış portal API |

- Korkut'un **tek persona/sistem talimatı** var (rol, sınırlar, "taslaktır" uyarısı);
  modüller bu personanın içinde uzmanlaşır.

## 8. Veri girişi (duruşma/müvekkil) — KARAR-2: Doğal dil + onay
- Korkut'a doğal dille yazılır: "Korkut, Ahmet Yılmaz boşanma dosyası 15 Temmuz 10:00
  Antalya 3. Aile Mahkemesi'nde".
- Korkut alanları **çıkarır** ve **yapılandırılmış özet** olarak gösterir.
- Kaydetmeden önce **§9'daki doğal dil onay diyaloğunu** uygular — kullanıcı onaylamadan
  DB'ye yazmaz; "şu tarihi düzelt" derse düzeltip tekrar onaya sunar.

## 9. Onay akışı — KARAR-4: Doğal dil diyaloğu (tüm kalıcı işlemler için)
Bu sadece dilekçeye değil, **kalıcı/önemli her işleme** uygulanır (duruşma kaydı,
dilekçe finalize, veri değişikliği).

```
Ajan işi hazırlar → ne yapacağını ÖZETLER → "Onaylıyor musunuz?" diye SORAR
   │   (durum: onay_bekliyor — henüz kalıcı değil)
   ▼
Kullanıcı doğal dille cevap verir:
   • "tamam / olur / onaylıyorum / evet"      → ajan işlemi KESİNLEŞTİRİR
   • "yok / dur / iptal"                       → işlemi YAPMAZ, bırakır
   • "şu tarihi düzelt / karşı tarafı ekle"    → ajan DEĞİŞİKLİĞİ uygular,
                                                  tekrar özetleyip yeniden onay ister
```

- Ajan onay/ret/düzeltme niyetini **doğal dilden** anlar (sabit komut yok).
- **Onay alınmadan hiçbir kalıcı işlem yapılmaz.** Belirsiz cevapta tekrar sorar.
- Dilekçe için ek olarak: değişmesi gereken yerleri ajan kendi **analiz edip önerebilir**
  ("şu maddede tarih eksik, eklememi ister misiniz?").

## 10. Hata / düşme davranışı
- Ollama kapalı + model=yerel → bulut'a düş, kullanıcıya sessiz, log'a düş.
- Provider hatası/timeout → ajan "şu an ulaşamadım, tekrar dener misin" der; çökmez.
- Redaction tespitinden emin değilse → **güvenli taraf**: şüpheli alanı da rumuzla.
- **Onaysız hiçbir kalıcı işlem yok** (§9). DB/dosya yazımı önce `onay_bekliyor`; ancak
  doğal dil onayıyla kesinleşir. Bu, tüm yazma işlemleri için çapraz ilkedir.

## 11. Tasarım kararları (kapandı)
- **KARAR-1 — Tetikleme:** ✅ Kanalda sadece `@Korkut` / Korkut'un thread'i; **DM'de her
  mesaj**. Kanalda etiketsiz mesajlar yok sayılır.
- **KARAR-2 — Veri girişi:** ✅ Doğal dil + yapılandırılmış özet + onay (§8).
- **KARAR-3 — Rumuz haritası:** ✅ Varsayılan: yalnızca RAM'de, istek sonrası silinir
  (asgari PII izi). Audit gerekirse şifreli kalıcı log opsiyon olarak eklenir.
- **KARAR-4 — Onay:** ✅ Doğal dil diyaloğu; onaysız kalıcı işlem yok (§9).
- **KARAR-5 — Uygulama DB'si:** ✅ Baştan Postgres (ayrı database).

## 12. Tools katmanı (ajan yetenekleri)
Her dış yetenek `tools/` altında **bağımsız, CLI'dan çalışan, kendi kendini belgeleyen**
bir araçtır. Korkut (uzmanlık modülleri) bunları toolset olarak kullanır.

### Klasör sözleşmesi (`tools/<ad>/`)
```
tools/ictihat_ara/
  ├─ __main__.py    # CLI girişi
  ├─ tool.yaml      # ad, açıklama, parametre şeması (JSON Schema) → function-calling
  ├─ README.md      # ne yapar, örnek girdi/çıktı, hangi uca gider
  └─ client.py      # vendor/'dan uyarlanmış httpx istemcisi (MIT notu korunur)
```

### CLI sözleşmesi (tertemiz, ajandan bağımsız)
```
python -m tools.ictihat_ara --sorgu "kira tespiti" --mahkeme yargitay --limit 10
  • Girdi: argüman veya stdin JSON
  • Çıktı: stdout'a TEK JSON nesnesi (insan için değil, makine için)
  • Log: stderr   • Başarı/hata: exit code (0/≠0)
```
Bu sayede her tool tek başına test edilir; ajan olmadan da çalışır.

### tool.yaml (örnek)
```yaml
ad: ictihat_ara
aciklama: "Yargıtay/Danıştay/Emsal kararlarında arama yapar (Bedesten API)."
parametreler:
  sorgu:   { tip: string, zorunlu: true }
  mahkeme: { tip: string, secenekler: [yargitay, danistay, emsal] }
  limit:   { tip: integer, varsayilan: 10 }
```

### Kayıt ve çağrı (registry)
- `core/tool_registry.py` açılışta `tools/`'u tarar, her `tool.yaml`'ı okur.
- Manifestler GPT'ye **function-calling tool'ları** olarak verilir (config'te hangi
  ajan hangi tool'a erişir: `config/tools.yaml`).
- GPT bir tool çağırınca registry onu çalıştırır (in-process import; gerekirse subprocess
  CLI) ve JSON sonucu modele döndürür.

### config/tools.yaml (hangi uzmanlık modülü neye erişir)
```yaml
# Korkut tek kimlik; tool'lar içeride uzmanlığa göre gruplanır.
durusma:   [durusma_ekle, durusma_listele]
dilekce:   [sablon_getir, dilekce_uret, ictihat_ara]
arastirma: [ictihat_ara, mevzuat_ara, karar_metin]
```

### Gizlilik
- Tool girdisi bulut modele gidecekse **redaction kuralları geçerli** (§5).
- İçtihat/mevzuat sorguları kamuya açık; ama müvekkil bağlamı eklenmişse o kısım rumuzlanır.

### İlk tool seti (Faz 5)
`ictihat_ara`, `mevzuat_ara`, `karar_metin` — `vendor/` istemcilerinden uyarlanır
(KAYNAKLAR.md §8). Sonraki tool'lar (duruşma, dilekçe) aynı sözleşmeyle eklenir.

## 13. Çalışma motoru (framework kararı)
- Korkut **kendi Python uygulamamız**; clawbot/Claude Code değil (o sadece *geliştirme*
  aracı — ürün kurulunca ona ihtiyaç yok, bağımsız servis).
- Ajan döngüsü **ince katman**: resmî provider SDK'ları (`openai`; sonra `anthropic`,
  `ollama`) + kendi yazdığımız küçük döngü. **Ağır orkestrasyon framework'ü yok**
  (LangChain/LangGraph vb.).
- Döngü (provider-bağımsız):
  ```
  mesajlar + tool şemaları → model
     ├─ model tool çağırdıysa → tool_registry.calistir → sonucu ekle → tekrar
     └─ final metin → döndür (Korkut kimliğiyle yaz)
  ```
- Tüm model çağrıları `core/router`'dan geçer → **tek noktada** redaction + provider
  seçimi + "yerel yoksa buluta düş".
- `core/providers/<ad>.py` her sağlayıcının function-calling formatını **ortak arayüze**
  çevirir → GPT↔Claude↔yerel swap'ı tek dosya.
- **Neden ince katman:** (1) provider-swap önceliğimiz, ağır framework zorlaştırır;
  (2) redaction tek chokepoint'te garanti; (3) döngü küçük (~150 satır), az bağımlılık =
  uzun ömür; (4) tool'lar zaten `tools/`+registry, framework tool soyutlaması gereksiz.

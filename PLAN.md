# Avukatlık Ofisi Asistanı — Uygulama Planı

> Durum: Tasarım onaylandı, kuruluma hazır. Bu doküman tüm kararların tek kaynağıdır.

## 1. Amaç
Bir avukatlık ofisinin günlük işlerini (duruşma hatırlatma, dilekçe taslağı, hukuki
araştırma) tek bir sohbet arayüzünden yürüten, veriyi ofiste tutan asistan sistemi.

## 2. İlkeler
- **Veri ofiste kalır.** Sohbet sunucusu (Mattermost) kendi makinemizde; `data/` asla
  dışarı/depoya çıkmaz, şifreli diskte durur.
- **Buluta giden her şey önce rumuzlanır.** İsim/TCKN/dosya no → token. Geri-açma
  eşlemesi yalnızca sunucuda. (Sağlayıcı GPT de olsa Claude da olsa kural aynı.)
- **Onaysız kalıcı işlem yok.** Ajan ne yapacağını özetler, doğal dille "onaylıyor
  musunuz?" diye sorar; onay alınmadan duruşma/dilekçe/veri kalıcılaşmaz. Düzeltme
  istenirse uygulayıp tekrar onaya sunar. (Detay: MIMARI.md §9)
- **Model seçimi config'ten.** Her iş → `yerel` veya `bulut`; tek satırla değiştirilir.
- **Sağlayıcı takılıp çıkarılabilir.** Bugün bulut = GPT; ileride Claude / yerel model.
- **Yetenekler tool'dur.** Her dış yetenek (içtihat/mevzuat arama, metin çekme...) `tools/`
  altında bağımsız bir araç: **CLI'dan tek başına çalışır**, kendi dökümantasyonunu taşır,
  ajanlara **toolset olarak configure edilir**. Yeni yetenek = yeni klasör. (Detay: MIMARI.md §12)

## 3. Mimari

```
#ofis  (TEK kanal, self-hosted Mattermost — veri ofisten çıkmaz)
   │   herkes buraya yazar
   ▼
Dağıtıcı servis  (tek Python süreci, kanalı WebSocket ile dinler)
   │
   ├─ Mesajda bir ajan etiketlenmişse → o ajan yanıtlar
   │     "@Defne yarınki duruşmaları listele"
   │
   └─ Etiketsiz mesaj → Kâtip (yönlendirici) niyeti anlar, doğru ajana verir
         "boşanma için cevap dilekçesi taslağı"  → Kerem'e
   │
   ▼
core/  (router + redactor + db)
   ├─ model: yerel  → Ollama         (donanım gelince; yokken bulut'a düşer)
   └─ model: bulut  → rumuzla → GPT  (ileride Claude/diğer)
```

**Kilit kural:** Mesaj başına **tek** ajan cevap verir. Etiketlersen o ajan; etiketlemezsen
Kâtip karar verir. Tüm ajanlar tek serviste çalışır ama Mattermost'ta ayrı bot
kullanıcısı (kendi adı + avatarı) olarak görünür.

> Not: İleride **çok ofis** (her ofis = bir kanal) eklenecek; şimdilik tek kanalla başlıyoruz.

## 4. Ajanlar

| Ad | Rol | İş | Model |
|----|-----|----|-------|
| **Kâtip** | Yönlendirici | Etiketsiz mesajları doğru ajana yöneltir + genel sohbet | bulut |
| **Defne** | Duruşma | Hatırlatma, takvim (ekle/listele) | yerel → şimdilik bulut |
| **Kerem** | Dilekçe | Şablon + taslak üretimi | bulut |
| **Bilge** | Araştırma | Mevzuat/içtihat arama + özet | bulut |

## 5. Dizin yapısı

```
hukuk-asistan/
├─ PLAN.md                     # bu doküman
├─ config/
│  ├─ model-routing.yaml       # backend seçimi + ajan→model
│  ├─ ajanlar.yaml             # ajan adları, botlar, persona
│  └─ redaction-rules.yaml     # rumuzlanacak alanlar
├─ data/                       # .gitignore — asla dışarı çıkmaz
│  └─ dosyalar/<dava-no>/      # dilekçeler, ekler, kararlar
│                              # (yapısal veri Postgres'te: asistan-db)
├─ core/
│  ├─ router.py                # model seçici (yerel/bulut), düşme kuralı
│  ├─ redactor.py              # rumuzla / geri-aç
│  ├─ providers/               # gpt.py, (sonra) claude.py, ollama.py
│  ├─ tool_registry.py         # tools/ manifestlerini okur → ajanlara toolset verir
│  └─ db.py
├─ tools/                      # ajan YETENEKLERİ — her biri CLI'dan tertemiz çalışır
│  ├─ README.md                # tüm tool'ların indeksi + CLI sözleşmesi
│  ├─ ictihat_ara/             # Yargıtay/Danıştay/Emsal arama (Bedesten)
│  │   ├─ __main__.py          #   CLI: python -m tools.ictihat_ara --sorgu "..."
│  │   ├─ tool.yaml            #   ad/açıklama/parametre şeması (function-calling)
│  │   └─ README.md            #   dökümantasyon (örnek girdi/çıktı)
│  ├─ mevzuat_ara/             # mevzuat.gov.tr arama
│  ├─ karar_metin/             # karar/mevzuat tam metin (Markdown)
│  └─ ...                      # yeni yetenek = yeni klasör (aynı sözleşme)
├─ vendor/                     # yargi-mcp + mevzuat-mcp (MIT) — kaynak/referans
├─ bridge/
│  └─ mattermost.py            # tek kanal dinleyici + ajan dağıtıcı + bot kimlikleri
├─ agents/
│  ├─ katip.py                 # yönlendirici (niyet sınıflama)
│  ├─ defne.py                 # duruşma
│  ├─ kerem.py                 # dilekçe
│  └─ bilge.py                 # araştırma
├─ templates/dilekce/          # Türk hukuku dilekçe şablonları (.docx/.md)
└─ scripts/cron/               # duruşma hatırlatma job'ı
```

## 6. Config örnekleri

`config/model-routing.yaml`
```yaml
backendler:
  bulut: gpt          # şimdilik OpenAI; ileride: claude / diğer
  yerel: ollama       # donanım gelince aktif

# Ollama kapalıyken "yerel" istenen işler otomatik bulut'a düşer + loglanır.
yerel_yoksa_buluta_dus: true

ajanlar:
  katip:  { model: bulut }
  defne:  { model: yerel }
  kerem:  { model: bulut }
  bilge:  { model: bulut }
```

`config/ajanlar.yaml`
```yaml
kanal: ofis
ajanlar:
  katip: { ad: "Kâtip", rol: yonlendirici }
  defne: { ad: "Defne", rol: durusma }
  kerem: { ad: "Kerem", rol: dilekce }
  bilge: { ad: "Bilge", rol: arastirma }
```

## 7. Rumuzlama (redaction)
- **Tespit:** TCKN (11 hane + algoritma kontrolü), telefon, IBAN, plaka, esas/dosya no
  (regex) + isim (Türkçe isim sözlüğü + kurallar; ileride yerel NER).
- **Rumuzla:** `Ahmet Yılmaz → [KİŞİ_1]`; eşleme `data/rumuz-eslemeleri/`'nde.
- **Geri-aç:** Bulut cevabındaki token'lar gerçek değerlerle değiştirilir.
- **Kural:** `yerel` → rumuzlanmaz (zaten çıkmıyor). `bulut` → **her zaman** rumuzlanır.

## 8. Fazlar

- **Faz 0 — Sunucu & temel:** Ubuntu 24.04 + LUKS disk şifreleme, Python 3.12 venv,
  Mattermost (Docker) + uygulama Postgres'i (asistan-db), OpenAI API anahtarı (env'de),
  dizin iskeleti, Postgres şeması.
  **Markalama:** kurulum scripti "Ofis adı?" sorar → Mattermost `SiteName`'e yazılır
  (mmctl/config.json), her yerde "Mattermost" yerine ofis adı görünür.
  *(Ollama yok — donanım gelince ayrı mini-faz.)*
- **Faz 1 — core:** Model router (+ "yerel yoksa buluta düş" kuralı) + rumuzlama + DB +
  **tool registry** (`tools/` sözleşmesi: CLI + tool.yaml → ajan toolset'i).
  Test: buluta hiçbir gerçek ad/TCKN gitmiyor; örnek tool CLI'dan çalışıyor.
- **Faz 2 — Mattermost köprüsü:** Tek kanal dinleyici, etiket/niyet ile ajan dağıtımı,
  4 bot kimliği (Kâtip/Defne/Kerem/Bilge).
- **Faz 3 — Defne (duruşma):** Manuel duruşma girişi + cron hatırlatma → kanala
  "yarınki duruşmalar" düşer.
- **Faz 4 — Kerem (dilekçe):** Şablon + bulut taslak (rumuzlu) → `python-docx` ile
  `.docx` → `data/dosyalar/` altına "onay bekliyor" etiketiyle.
- **Faz 5 — Bilge (araştırma):** Veri tool'larını `tools/` altında inşa et —
  `ictihat_ara`, `mevzuat_ara`, `karar_metin` (vendor/ httpx istemcilerinden, MIT). Her
  biri CLI'dan tertemiz + tool.yaml + README. Bilge bunları toolset olarak kullanır →
  kaynaklı özet. Kaynaklar: KAYNAKLAR.md.
- **Sonra:** Yerel model (Ollama + GPU), UYAP entegrasyonu, yetkilendirme/çok kullanıcı.

## 9. Teknoloji yığını (Python)

| İş | Kütüphane |
|----|-----------|
| Bulut LLM | `openai` (sonra `anthropic`) |
| Yerel LLM (sonra) | `ollama` |
| Sohbet köprüsü | `mattermostdriver` (WebSocket) |
| DB | Postgres (`psycopg` / `SQLAlchemy`) |
| Dilekçe (Word) | `python-docx` |
| Zamanlama | sistem `cron` (+ `APScheduler` opsiyon) |
| Araştırma çekme | `httpx` + `selectolax` / `beautifulsoup4` |
| Rumuzlama | regex + isim sözlüğü (ileride yerel NER) |

## 10. KVKK / hukuki kontrol listesi
- [ ] OpenAI ile veri işleme şartları / DPA durumu netleştir.
- [ ] Müvekkil aydınlatma metni + açık rıza şablonu (yapay zekâ ile işleme).
- [ ] Baro reklam/etik kurallarına uygunluk.
- [ ] Disk şifreleme (LUKS) + erişim kontrolü + yedekleme politikası.
- [ ] Rumuzlama testleri (PII sızıntısı yok kanıtı).

## 11. Açık konular
- **Bildirim:** Duruşma hatırlatması sadece Mattermost kanalına mı, ayrıca e-posta mı?
- **UYAP:** Resmî API erişimi var mı, yoksa yarı-otomatik mi? (Faz sonrası ayrı plan.)
- **Çok kullanıcı:** Tek avukat mı, ekip mi? (Yetkilendirme gerekir mi?)
- **Çok ofis (sonra):** Her ofis = bir kanal; ofisi organizasyon boyutu olarak ekleme.

## 12. Kapsam kararları (sabit)
- **Markalama: Seviye 1** — kurulumda ofis adı sorulur, Site Adı olarak yazılır.
  Ücretsiz, lisans sorunu yok. Logo/white-label ileride (ürünleşirse).
- **Dağıtım: tek ofis** — şimdilik kendi ofise kurulum. Çok-ofise dağıtılan paket sonra.
- **White-label lisans notu:** Açık kaynak (Team) sürümde kodu rebrand etmek serbest;
  "Custom Branding" Enterprise'a kapalı (webapp'i kendin düzenle), AGPL paylaşım
  yükümlülüğü yalnızca *dağıtım/servis* durumunda — tek ofiste geçerli değil.

## 13. Donanım (yerel model için, ileride)
Yerel modele geçişte hedef: ~24 GB VRAM'li GPU (RTX 3090 ikinci el / 4090) →
`qwen2.5:32b` Türkçe hukuki metinde buluta en yakın yerel sonucu verir.
Şimdilik **sıfır donanım yatırımı**, GPT üzerinden başlanır.

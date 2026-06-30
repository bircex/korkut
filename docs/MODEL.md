# Model — sağlayıcı kataloğu, bulut/yerel ayrımı, UI'dan kurulum

## Amaç

Korkut'un beynini (LLM) seçmenin **tek yolu UI** olsun. Bugün iki ayrı yol var: bir yanda
`.env` + `config/model-routing.yaml` ile gelen **yerleşik varsayılan** (Groq/Claude/GPT), öbür
yanda `OfficeModel` tablosu + Ayarlar→Model ile gelen **ofis özel modeli** (sadece Ollama
preset'leri, `is_local` sabit). Bu tutarsızlık kalkacak:

- **Hazır bir sağlayıcı kataloğu** gelir (OpenAI, Anthropic, Groq, Google, Mistral, DeepSeek, xAI,
  OpenRouter + yerel: Ollama, LM Studio, vLLM, llama.cpp). Her sağlayıcının adresi, modelleri,
  anahtar gerektirip gerektirmediği önceden tanımlı.
- **Bulut ve yerel ayrılır.** Bu ayrım kozmetik değil, **redaksiyon sınırıdır** (ARCHITECTURE §5):
  bulut → metin maskelenip gönderilir, yerel → makineden çıkmadığı için maskelenmez.
- **Model yalnızca UI'dan eklenir.** `.env`'deki model anahtarları ve YAML'deki sağlayıcı
  bağlama kaldırılır; anahtar UI'dan girilir, DB'de write-only saklanır.

## Kararlar

- **Tek aktif model.** Ofis katalogdan tek model seçer — bulut **veya** yerel. Çoklu-tier /
  fallback yok (router'ın `single_model_router` deseni). Sadelik öncelik.
- **Yerleşik varsayılan tamamen kaldırılır.** Model tanımlanmadan Korkut çalışmaz; ilk kurulumda
  model seçimi zorunludur (owner için onboarding adımı). `.env`'de hiçbir model anahtarı kalmaz.
- **Katalog tek doğruluk kaynağı.** Backend'de statik liste; frontend bunu `/api/model/catalog`
  ucundan çeker. UI'a sabit kod (preset) yazılmaz. Yeni sağlayıcı = katalogda bir satır.
- **Wire format'a göre sağlayıcı sınıfı.** OpenAI-uyumlu olan herkes `OpenAICompatProvider`;
  Anthropic `ClaudeProvider`. Katalog girişi hangisini kullanacağını söyler.
- **Düzenleme yetkisi:** yalnızca ofis sahibi (owner) — bugünkü davranış korunur.

## Sağlayıcı kataloğu (`core/providers/catalog.py`, yeni)

Statik veri; her giriş bir `ProviderSpec`:

```
ProviderSpec
  id            str    katalog kimliği, ör. "openai", "ollama"
  name          str    ekranda görünen ad
  kind          str    "cloud" | "local"        # redaksiyon sınırı bundan türer
  wire          str    "openai" | "anthropic"   # hangi Provider sınıfı
  base_url      str    varsayılan /v1 adresi (yerelde düzenlenebilir)
  requires_key  bool   bulut: True, yerel: çoğu False
  models        list   [{id, label, note?}]     # katalogdan seçim; "özel" elle yazım hep açık
  docs_url      str?   anahtar/kurulum sayfası
  icon          str    UI ikonu (Tabler adı)
```

Başlangıç içeriği:

**Bulut** (`requires_key: True`, `kind: "cloud"`)

| id | wire | base_url | örnek modeller |
|----|------|----------|----------------|
| openai | openai | `https://api.openai.com/v1` | gpt-4o, gpt-4o-mini, o4-mini |
| anthropic | anthropic | `https://api.anthropic.com` | claude-opus-4-8, claude-sonnet-4-6, claude-haiku-4-5 |
| groq | openai | `https://api.groq.com/openai/v1` | llama-3.3-70b-versatile, llama-3.1-8b-instant, moonshotai/kimi-k2-instruct |
| google | openai | `https://generativelanguage.googleapis.com/v1beta/openai/` | gemini-2.5-pro, gemini-2.0-flash |
| mistral | openai | `https://api.mistral.ai/v1` | mistral-large-latest, codestral-latest |
| deepseek | openai | `https://api.deepseek.com/v1` | deepseek-chat, deepseek-reasoner |
| xai | openai | `https://api.x.ai/v1` | grok-2 |
| openrouter | openai | `https://openrouter.ai/api/v1` | (gateway — model elle) |
| custom | openai | (boş, elle) | (elle) — herhangi OpenAI-uyumlu geçit |

**Yerel** (`requires_key: False`, `kind: "local"`)

| id | base_url | örnek modeller |
|----|----------|----------------|
| ollama | `http://localhost:11434/v1` | llama3.3, qwen2.5, mistral, gemma2, deepseek-r1 |
| lmstudio | `http://localhost:1234/v1` | (yüklü model) |
| vllm | `http://localhost:8000/v1` | (sunulan model) |
| llamacpp | `http://localhost:8080/v1` | (yüklü model) |

> Not: model listeleri "ilham/kısayol"dur, kapalı küme değil. UI her sağlayıcıda "Özel — elle
> yaz" seçeneğini açık tutar; backend gelen `model` string'ini olduğu gibi kullanır.

## Veri modeli (`api/models.py`)

`OfficeModel` korunur, iki kolon eklenir, `is_local` türetilir:

```
office_model
  id            PK
  office_id     int   FK offices — UNIQUE (ofis başına tek aktif model)
  enabled       bool
  provider      str   katalog id (ör. "openai", "ollama")   # YENİ
  tier          str   "cloud" | "local"                      # YENİ (kataloğun kind'inden gelir)
  model         str   model adı (katalog ya da elle)
  base_url      str   OpenAI-uyumlu /v1 (katalog default'u, yerelde düzenlenebilir)
  api_key       Text  write-only; geri dönülmez (yerelde boş)
  updated_at    datetime
```

- `tier` redaksiyon kararını taşır → `is_local` kolonu kalkar (geriye dönük: var olan satırlarda
  `is_local=True` → `tier="local"`, `provider="ollama"`; aksi `tier="cloud"`, `provider="custom"`).
- `enabled=False` ise model yok demektir (artık varsayılana düşmez — Korkut kurulum ister).

## Router & redaksiyon (`core/router.py`, `brain/agent.py`)

- `single_model_router(cfg)` korunur; `cfg.local` yerine `cfg.tier == "local"` ile çalışır ve
  `wire`'a göre provider seçer: `openai → OpenAICompatProvider`, `anthropic → ClaudeProvider`.
- `brain/agent.py`'deki `get_router()` (çok-sağlayıcılı yerleşik varsayılan) **silinir**.
  `answer()` her zaman `model_cfg` ister; `model_cfg` None ise net hata → UI'da "önce model seç".
- `ModelCfg`'e `wire` alanı eklenir (`tier` zaten `local` boolean'ı yerine geçer).
- Redaksiyon davranışı değişmez: `tier=="cloud"` → `redact → provider → restore`;
  `tier=="local"` → doğrudan.

## API (`api/app.py`)

| Uç | Değişiklik |
|----|-----------|
| `GET /api/model/catalog` | **Yeni.** Katalog listesi (anahtarsız, salt-okunur). Owner gerekmez. |
| `GET /api/model` | Alanlara `provider`, `tier` eklenir. |
| `PUT /api/model` | Gövdeye `provider`, `tier` eklenir; `is_local` çıkar. `enabled` ise `provider` + `model` + (bulutsa) `api_key` doğrulanır. `base_url` katalogdan default'lanır, yerelde override edilebilir. |
| `DELETE /api/model` | Davranış aynı ama artık "varsayılana dön" değil → "model tanımsız" (kurulum gerekir). |
| Yeni: `POST /api/model/test` | (opsiyonel) Bağlantıyı test eder — adrese küçük bir istek atıp erişilebilirliği döner. UI'daki "Bağlantıyı test et". |

## UI (`frontend/src/components/ModelSettings.tsx`)

- **Bulut / Yerel segment kontrolü** üstte. Seçim, katalogtan ilgili `kind`'i filtreler.
- **Sağlayıcı ızgarası** katalogtan dinamik (sabit PRESETS silinir). Seçilen sağlayıcı vurgulanır.
- **Model** alanı: o sağlayıcının `models` listesinden seçim + "Özel — elle yaz".
- **API anahtarı** alanı yalnızca `requires_key` ise gösterilir; write-only, "kayıtlı" durumu.
- **Sunucu adresi** yerel sağlayıcılarda düzenlenebilir input; bulutta katalog default'u salt-görünür.
- **Redaksiyon rozeti:** bulut seçiliyken "kişisel veriler maskelenir (KVKK)" uyarısı; yerelde
  "her şey ofis sunucusunda kalır" bilgisi.
- **Bağlantıyı test et** + Kaydet/Kaldır. Onboarding'de (model yokken) bu ekran zorunlu adım.

## Env / kalıntı temizliği

**Silinecek**

- `.env` / `.env.example`: `GROQ_API_KEY`, `GROQ_MODEL`, `ANTHROPIC_API_KEY`, `ANTHROPIC_MODEL`,
  `OPENAI_API_KEY`, `OPENAI_MODEL` — model anahtarları artık UI/DB'de.
- `config/model-routing.yaml`: `backends.cloud/local` sağlayıcı bağlama ve specialty→tier eşlemesi
  (tek aktif model her specialty'yi karşılar) — dosya kaldırılır ya da boşaltılır.
- `core/providers/groq.py`, `core/providers/gpt.py`'deki `os.getenv(...)` model/anahtar
  default'ları → katalog değerleri. Groq artık katalogta bir bulut girişi; ayrı `GroqProvider`
  gerekmez (OpenAI-uyumlu base_url + UI anahtarı yeter). `gpt.py` wire serileştirmesi
  `OpenAICompatProvider`'ın temeli olarak kalır.
- `brain/agent.py`: `get_router()`, `ClaudeProvider()/GptProvider()/GroqProvider()` ile kurulan
  yerleşik Router.

**Kalır** (altyapı, model değil)

- `KORKUT_HOST`, `KORKUT_PORT`, `KORKUT_SECRET`, `KORKUT_SECURE_COOKIES`, `KORKUT_TRUST_PROXY`,
  `KORKUT_DB_URL`, `POSTGRES_PASSWORD`.

**Dokümana yansıyacak:** `docs/LOCAL_TRIAL.md` ve `README.md`'deki "`.env`'e ANTHROPIC_API_KEY
koy" adımları → "uygulamayı aç, owner kaydol, Ayarlar→Model'den sağlayıcı seç" akışıyla değişir.

## Faz planı

1. **Katalog + veri modeli.** `catalog.py`, `OfficeModel`'e `provider/tier`, `ModelCfg.wire`,
   migrasyon (`is_local → tier/provider`).
2. **Router/agent.** `single_model_router` wire'a göre provider; `get_router()` ve yerleşik
   varsayılanın silinmesi; `answer()` model zorunlu.
3. **API.** `/api/model/catalog`, `GET/PUT/model` alan güncellemeleri, opsiyonel `/test`.
4. **UI.** Bulut/yerel segment, dinamik sağlayıcı ızgarası, redaksiyon rozeti, onboarding zorunlu adım.
5. **Temizlik + dokümanlar.** `.env(.example)`, `model-routing.yaml`, eski provider default'ları;
   `LOCAL_TRIAL.md` / `README.md` / `ARCHITECTURE.md §13` güncellemesi.

## Açık sorular

- **Onboarding zorunluluğu:** owner model seçene kadar sohbet kilitli mi, yoksa "model seç"
  uyarısıyla yumuşak engelleme mi? (Öneri: ilk girişte zorunlu adım.)
- **`/api/model/test`** ilk sürüme girsin mi, sonraya mı? (UI'da yer ayrıldı.)
- **Specialty→tier** bilgisi ileride çoklu-model'e dönerse geri gelecek; şimdilik tamamen
  kaldırmak yerine kodda yorum olarak izini bırakalım mı?

# Dilekçe Üretim Akışı (Drafting Flow)

Bir sohbet mesajından tam dilekçeye giden uçtan uca akış ve hangi dosyanın neyi
tanımladığı. Şablon katmanı `templates/` altında, çalışma zamanı `backend/src/korkut/api/`
altındadır.

## Dosya sorumlulukları (tek kaynak ilkesi)

| Dosya | Tanımlar | Birim |
|---|---|---|
| `templates/petition/catalog.json` | Dilekçe **kimliği** (id, ad, mahkeme, kategori) | type_id (126) |
| `templates/petition/_base.json` + `FORMAT.md` | Dilekçe **biçimi** (HMK m.119 bölüm dizilimi) | ortak |
| `templates/petition/examples/<id>.md` | **Altın örnek** (few-shot / tema) | type_id |
| `templates/intake/<id>.json` | Tipe **özel sorular** (sorular/kosullar/kontroller/zenginlestir) | type_id (ince) |
| `templates/mahkemeler.json` | **Mahkeme tipleri** kanonik listesi (görev) | mahkeme (28) |
| `templates/dava-workflow.json` | **Akış omurgası** + arketip mirası (deliller/görev/yetki/süre) | dava_turu (11) |

**Miras zinciri** — deliller/görev/süre tekrarını önler:

```
type_id ──catalog.json──▶ kategori ──dava-workflow.kategori_eslesme──▶ dava_turu
        ──dava_turleri[dava_turu]──▶ { deliller, gorevli_mahkeme, yetki, dava_sarti, kritik_sureler }
```

Böylece 126 tipin 118'i delillerini 11 arketipten miras alır; `intake/<id>.json` ince kalır.
(8 tip — Kanun Yolları, AYM/AİHM — `null` kategori; mirası yoktur, tipe özel intake gerekir.)

## Mevcut akış (kodda gerçekte olan)

```
sohbet metni
  │
  ▼  petitions.detect(text)                      backend/.../petitions.py:254  (app.py:862)
type_id
  │
  ▼  _make_draft → petitions.draft_prompt(type_id)   app.py:1294 / petitions.py:324
biçim-farkında TEK ATIŞLIK talimat (catalog + _base.json bölümleri)
  │
  ▼  agent.answer(prompt)
tam dilekçe taslağı (ProposedEdit)
```

> ⚠️ **`petitions.intake()` (petitions.py:178) tanımlı ama hiçbir yerden çağrılmıyor.**
> `draft_prompt` şu an intake'i, delilleri, `dava-workflow.json`'u ve `mahkemeler.json`'u
> kullanmıyor. Aşağıdaki "soru sor, sonra yaz" davranışı **tasarım** — henüz aktif değil.

## Hedef akış (intake + deliller devrede)

```
detect(text) → type_id
  │
  ▼  intake(type_id)  +  arketip mirası (kategori_eslesme → deliller/görev/süre)
eksik sorular + zorunlu delil soruları
  │
  ▼  Korkut TEK MESAJDA sorar (belgeyi yazmaz)        ← aşama 3 (intake)
cevaplar + delil envanteri
  │
  ▼  kosullar değerlendirilir → ek soru/bölüm/uyarı
  ▼  zenginlestir → canlı mevzuat/emsal (tools.yaml)  ← aşama 4 (araştırma)
  ▼  draft_prompt + cevaplar + deliller → taslak       ← aşama 5-6 (kurgu + delil işleme)
  │     vakıa↔delil (deliller[].ispat); eksik+celp=true → "celbi talep olunur"
  ▼  kontroller + HMK m.119 kritik bent taraması       ← aşama 7
```

Aşama tanımları: `templates/dava-workflow.json` → `akis.asamalar`.

## Aktive etmek için gereken backend değişikliği (henüz yapılmadı)

1. **Çözücü**: `type_id → kategori → dava_turu` (kategori_eslesme) + arketip deliller/görev birleştirme.
2. **Intake adımı**: `_make_draft` öncesi, eksik `sorular` + zorunlu `deliller[].soru` varsa
   taslağı yazmadan **tek mesajda sor**; cevaplar gelince devam et.
3. **draft_prompt zenginleştirme**: intake cevaplarını + delil envanterini prompt'a ekle;
   DELİLLER bölümünü mevcut/celp ayrımıyla kurdur.

> Not: İçerik (126 ince intake) ile wiring **ayrı işlerdir**; biri olmadan diğeri çalışmaz.
> Önerilen sıra: bir kategoride (ör. Aile Hukuku) wiring + intake'i birlikte pilotlayıp
> gerçek çalıştırmak, sonra kalan kategorileri seri üretmek.

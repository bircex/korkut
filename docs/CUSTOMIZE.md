# Özelleştir — per-office / per-user şablon temaları

## Amaç

Her dilekçe ve sözleşme tipinin bir **teması** (örnek markdown) var; LLM bundan **ilham** alıp
belgeyi olaya göre sıfırdan yazar (mekanik placeholder doldurma / "replacement" **yok**). Bir
hukuk ofisi bu temaları kendi tarzına göre **özelleştirebilmeli**: düzenlemezse varsayılan tema,
düzenlerse kendi versiyonu kullanılır. Temalar dosyalardan gelir ve her açılışta DB'ye senkronlanır.

## Kararlar

- **Tek özelleştirilebilir birim: tema** (per-type markdown). Dilekçe + sözleşme, ikisi de.
- **Replacement / intake Q&A yok.** Eksik bilgiyi LLM konuşarak toplar; zorunlu unsurlar/koşullar
  (örn. HMK m.119, "vasiyetname varsa TMK m.598 paragrafı ekle") **temanın içine** yazılır.
  (Mevcut `templates/intake/*.json` dosyaları silinmez ama bu akışa bağlanmaz.)
- **Üç katmanlı çözümleme:** `user override > office override > default`
  (mevcut `StyleProfile` / `_effective_style` deseninin aynısı).
- **Düzenleme yetkisi:** her üye hem kişisel hem ofis-geneli override yazabilir (rol kısıtı yok).
- **Senkron:** default'lar her boot'ta dosyadan upsert; override'lara asla dokunulmaz.

## Veri modeli (2 yeni tablo, `api/models.py`)

```
template_default
  id            PK
  type_id       str   (katalog kimliği, ör. "anlasmali-bosanma") — UNIQUE
  kind          str   ("dilekce" | "sozlesme")
  content       Text  (tema markdown)
  source_hash   str   (dosya değişmediyse yazma)
  updated_at    datetime

template_override
  id            PK
  office_id     int?  (FK offices) — ofis-geneli override için dolu
  user_id       int?  (FK users)   — kişisel override için dolu
  type_id       str
  content       Text  (özelleştirilmiş tema)
  updated_at    datetime
  UNIQUE(user_id, type_id)        # kişisel: tip başına 1
  UNIQUE(office_id, type_id) WHERE user_id IS NULL   # ofis: tip başına 1 (partial index)
```

## Senkron mekanizması (`api/store.py`)

`engine()` init'inde (init_db + tenant migrasyonundan sonra) `sync_template_defaults()`:
- `templates/petition/examples/*.md` → `template_default(kind="dilekce")` upsert
- `templates/contract/examples/*.md` → `template_default(kind="sozlesme")` upsert
- `source_hash` (içerik hash'i) değişmemişse yazma; **`template_override`'a hiç dokunma**.
- Dosyası silinen default'lar opsiyonel olarak budanabilir (v1: bırak).

## Çözümleme (`api/petitions.py` → app katmanı)

`petitions.example(type_id)` yerine user/office bağlamı alan:
```python
def effective_theme(s, user, type_id) -> str | None:
    # 1) user override (user_id == user.id)
    # 2) office override (office_id == user.office_id, user_id IS NULL)
    # 3) template_default
```
`app.py/_make_draft` bunu kullanır; mevcut "ÖRNEK FORMAT'a birebir uy + `[köşeli parantez]`
bırak" dili **kaldırılır** → "bu temadan **ilham al**, olaya göre sıfırdan yaz; tema yer
tutucularını kopyalama" mantığına döner.

## API uçları (`api/app.py`)

- `GET    /api/templates?kind=dilekce|sozlesme` → tip listesi (kataloğdan) + rozet:
  `{type_id, ad, kategori, hasOfficeOverride, hasUserOverride}`
- `GET    /api/templates/{type_id}` → `{default, officeOverride?, userOverride?, effective}`
- `PUT    /api/templates/{type_id}` `{scope:"user"|"office", content}` → override upsert
- `DELETE /api/templates/{type_id}?scope=user|office` → override sil (alt katmana dön)
- Hepsi office-scope guard'lı (kullanıcının kendi ofisi); tenant izolasyonu korunur.

## UI — "Özelleştir" alanı (`frontend/`)

- Yeni sekme/sayfa. Kategori akordeon + **arama kutusu** (126 dilekçe + 58 sözleşme — filtre şart).
- Tipe tıkla → markdown editör, default tema dolu gelir. **Scope toggle: Kişisel / Ofis geneli.**
  Kaydet → override. **"Varsayılana dön"** → override sil.
- Rozet: hangi tiplerde kişisel/ofis override var.

## Kapsam dışı (v1)

- Yeni tip ekleme/silme (sadece mevcut tiplerin teması düzenlenir).
- Intake / structured Q&A (dosyalar durur, bağlanmaz).
- Tema sürüm geçmişi / diff (gerekirse sonra).

## Doğrulama

1. Boot'ta `template_default` dosya sayısı kadar dolu; ikinci boot'ta `source_hash` ile yazma yok.
2. Kişisel override yaz → `_make_draft` o temayı kullanıyor; sil → ofis/default'a düşüyor.
3. İki ofis izolasyonu: A ofisinin override'ı B ofisinde görünmüyor.
4. Tüm gate'ler (ruff/format/mypy/pytest, frontend lint/build) yeşil; yeni tablolar için testler.

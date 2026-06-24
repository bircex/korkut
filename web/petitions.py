"""Legal-document catalogs (dilekçe + sözleşme) + format guidance for the Canvas drafting flow.

Loads the data-driven catalogs under `templates/petition/` and `templates/contract/` and the base
section skeletons once, infers a document type from free chat text (`detect`), and turns a type
into a format-aware drafting prompt (`draft_prompt`). Keeping the format rules here (one place,
data-driven) means detection and the draft prompt never drift from the catalogs. See the
`FORMAT.md` next to each catalog.
"""

from __future__ import annotations

import json
import re
from functools import lru_cache
from pathlib import Path

_TPL = Path(__file__).resolve().parent.parent / "templates"
_DIRS = {"dilekce": _TPL / "petition", "sozlesme": _TPL / "contract"}

# Turkish fold for matching (diacritics → ascii), so "boşanma" matches "bosanma".
# Both cases are mapped explicitly: translating before .lower() means uppercase titles
# (e.g. "DİLEKÇESİ") fold correctly, while side-stepping the locale-specific "İ".lower() pitfall.
_FOLD = str.maketrans("çğıİöşüâîûÇĞÖŞÜÂÎÛ", "cgiiosuaiucgosuaiu")
_STOP = {
    "dava",
    "dilekce",
    "dilekcesi",
    "talebi",
    "talep",
    "karsi",
    "dair",
    "hakkinda",
    "nedeniyle",
    "nedenli",
    "ozgu",
    "yolu",
    "olmadigi",
    "kararina",
    "beyan",
    "yer",
    "tesisi",
    "tedbir",
    "tedbiri",
    "veya",
    "iliskin",
    "sozlesme",
    "sozlesmesi",
    "noterde",
}

# Words that signal which kind the user means, so an ambiguous root (e.g. "kira") routes right.
_SIG_SOZLESME = ("sozlesme", "kontrat", "mukavele", "protokol", "nda", "ibraname", "taahhutname")
_SIG_DILEKCE = (
    "dilekce",
    "dava",
    "sikayet",
    "mahkeme",
    "itiraz",
    "temyiz",
    "istinaf",
    "savci",
    "icra takib",
)

# Curated high-precision patterns: ALL words (folded) must appear. Order matters — specific first.
_PATTERNS: dict[str, list[tuple[tuple[str, ...], str]]] = {
    "dilekce": [
        (("anlasmali", "bosanma"), "anlasmali-bosanma"),
        (("bosanma",), "cekismeli-bosanma"),
        (("ise", "iade"), "ise-iade"),
        (("kidem",), "iscilik-alacaklari"),
        (("iscilik",), "iscilik-alacaklari"),
        (("isten", "cikar"), "iscilik-alacaklari"),
        (("fazla", "mesai"), "fazla-mesai"),
        (("velayet",), "velayet-davasi"),
        (("nafaka", "artir"), "nafaka-artirim"),
        (("nafaka",), "tedbir-nafakasi"),
        (("itirazin", "iptal"), "itirazin-iptali"),
        (("menfi", "tespit"), "menfi-tespit"),
        (("tapu", "iptal"), "tapu-iptal-tescil"),
        (("ihtarname",), "ihtarname"),
        (("tahliye",), "tahliye-temerrut"),
        (("kira", "tespit"), "kira-tespiti"),
        (("trafik",), "trafik-kazasi-tazminat"),
        (("suc", "duyuru"), "suc-duyurusu"),
        (("sikayet",), "sikayet-dilekcesi"),
        (("veraset",), "veraset-ilami"),
        (("mirascilik",), "veraset-ilami"),
        (("miras", "ret"), "mirasin-reddi"),
        (("istinaf",), "istinaf-basvuru"),
        (("temyiz",), "temyiz-basvuru"),
        (("isim", "degis"), "isim-soyisim-degisikligi"),
        (("soyad", "degis"), "isim-soyisim-degisikligi"),
    ],
    "sozlesme": [
        (("bosanma", "protokol"), "bosanma-protokolu"),
        (("bosanma",), "bosanma-protokolu"),
        (("gizlilik",), "gizlilik-nda"),
        (("nda",), "gizlilik-nda"),
        (("isyeri", "kira"), "isyeri-kira"),
        (("dukkan", "kira"), "isyeri-kira"),
        (("konut", "kira"), "konut-kira"),
        (("kira",), "konut-kira"),
        (("belirsiz", "sureli"), "belirsiz-sureli-is"),
        (("belirli", "sureli"), "belirli-sureli-is"),
        (("kismi", "sureli"), "kismi-sureli-is"),
        (("uzaktan", "calisma"), "uzaktan-calisma"),
        (("rekabet", "yasak"), "rekabet-yasagi"),
        (("ibraname",), "ibraname"),
        (("staj",), "staj-sozlesmesi"),
        (("danismanlik",), "danismanlik"),
        (("avukatlik", "ucret"), "avukatlik-ucret"),
        (("vekalet",), "vekalet"),
        (("eser",), "eser-sozlesmesi"),
        (("istisna",), "eser-sozlesmesi"),
        (("hizmet",), "hizmet-sozlesmesi"),
        (("komisyon",), "komisyon"),
        (("tek", "satici"), "distributorluk"),
        (("distributor",), "distributorluk"),
        (("bayilik",), "bayilik"),
        (("franchise",), "franchise"),
        (("acente",), "acentelik"),
        (("tedarik",), "tedarik"),
        (("hisse", "devir"), "hisse-devri"),
        (("hisse", "devri"), "hisse-devri"),
        (("pay", "devir"), "hisse-devri"),
        (("adi", "ortaklik"), "adi-ortaklik"),
        (("kat", "karsilig"), "kat-karsiligi-insaat"),
        (("arsa", "pay"), "kat-karsiligi-insaat"),
        (("satis", "vaad"), "gayrimenkul-satis-vaadi"),
        (("arac", "satis"), "arac-satis"),
        (("trampa",), "trampa"),
        (("bagislama",), "bagislama"),
        (("temlik",), "alacak-temliki"),
        (("lisans",), "lisans-sozlesmesi"),
        (("telif",), "telif-devri"),
        (("yazilim",), "yazilim-gelistirme"),
        (("saas",), "saas-sla"),
        (("sla",), "saas-sla"),
        (("veri", "isleme"), "veri-isleme-kvkk"),
        (("kvkk",), "veri-isleme-kvkk"),
        (("kefalet",), "kefalet"),
        (("kefil",), "kefalet"),
        (("rehin",), "rehin"),
        (("leasing",), "finansal-kiralama"),
        (("finansal", "kiralama"), "finansal-kiralama"),
        (("borc", "ikrar"), "borc-ikrari"),
        (("odeme", "taahhut"), "borc-ikrari"),
        (("niyet", "mektub"), "niyet-mektubu-loi"),
        (("loi",), "niyet-mektubu-loi"),
        (("sulh",), "sulh-uzlasma"),
        (("cerceve",), "cerceve-sozlesme"),
        (("cezai", "sart"), "cezai-sart-protokol"),
        (("mal", "rejim"), "mal-rejimi-sozlesmesi"),
        (("olunceye", "kadar"), "olunceye-kadar-bakma"),
        (("satis",), "tasinir-satis"),
    ],
}

_PET_CRITICAL = {"119/d", "119/e-f", "119/f", "119/g", "119/a"}


@lru_cache(maxsize=4)
def _catalog(kind: str) -> dict:
    return json.loads((_DIRS[kind] / "catalog.json").read_text(encoding="utf-8"))


@lru_cache(maxsize=4)
def _base(kind: str) -> dict:
    return json.loads((_DIRS[kind] / "_base.json").read_text(encoding="utf-8"))


_INTAKE = _TPL / "intake"


@lru_cache(maxsize=128)
def intake(type_id: str) -> dict | None:
    """The intake schema (sorular/koşullar/kontroller/zenginleştir) for a type, or None.

    See templates/intake/FORMAT.md. Missing file → None (Korkut falls back to one-shot drafting).
    """
    path = _INTAKE / f"{type_id}.json"
    try:
        return json.loads(path.read_text(encoding="utf-8")) if path.exists() else None
    except (OSError, json.JSONDecodeError):
        return None


@lru_cache(maxsize=1)
def _index() -> dict[str, dict]:
    """type id -> {ad, ref, kategori, tur}. ref = mahkeme (dilekçe) | mevzuat (sözleşme)."""
    out: dict[str, dict] = {}
    for kind in _DIRS:
        for cat in _catalog(kind).get("kategoriler", []):
            for d in cat.get("dilekceler", []):
                out[d["id"]] = {
                    "ad": d["ad"],
                    "ref": d.get("mahkeme") or d.get("mevzuat"),
                    "kategori": cat["kategori"],
                    "tur": kind,
                }
    return out


def _grouped(kind: str) -> list[dict]:
    return [
        {
            "kategori": cat["kategori"],
            "dilekceler": [
                {
                    "id": d["id"],
                    "ad": d["ad"],
                    "ref": d.get("mahkeme") or d.get("mevzuat"),
                    "sik": bool(d.get("sik")),
                }
                for d in cat.get("dilekceler", [])
            ],
        }
        for cat in _catalog(kind).get("kategoriler", [])
    ]


def categories() -> list[dict]:
    """Petition (dilekçe) catalog grouped by category."""
    return _grouped("dilekce")


def contract_categories() -> list[dict]:
    """Contract (sözleşme) catalog grouped by category."""
    return _grouped("sozlesme")


def get(type_id: str) -> dict | None:
    return _index().get(type_id)


def example(type_id: str) -> str | None:
    """Curated golden example markdown for a type (few-shot), if present under <kind>/examples/."""
    info = get(type_id)
    if not info:
        return None
    path = _DIRS[info["tur"]] / "examples" / f"{type_id}.md"
    try:
        return path.read_text(encoding="utf-8") if path.exists() else None
    except OSError:
        return None


def _fold(s: str) -> str:
    return s.translate(_FOLD).lower()


def detect(text: str) -> str | None:
    """Infer a document type (dilekçe or sözleşme) from free chat text, or None if unsure.

    A signal word ("sözleşme"/"dava" …) picks the kind; otherwise both pools are tried. Curated
    patterns first (high precision), then a generic fallback scoring catalog names by word overlap.
    """
    t = _fold(text or "")
    if not t:
        return None
    con = any(sig in t for sig in _SIG_SOZLESME)
    pet = any(sig in t for sig in _SIG_DILEKCE)
    if con and not pet:
        pools = ["sozlesme"]
    elif pet and not con:
        pools = ["dilekce"]
    else:
        pools = ["dilekce", "sozlesme"]

    for kind in pools:
        for words, type_id in _PATTERNS[kind]:
            if all(w in t for w in words) and type_id in _index():
                return type_id

    best, best_id = 0, None
    for tid, info in _index().items():
        if info["tur"] not in pools:
            continue
        words = {
            w for w in re.findall(r"[a-z]+", _fold(info["ad"])) if len(w) >= 4 and w not in _STOP
        }
        hits = sum(1 for w in words if w in t)
        if hits > best:
            best, best_id = hits, tid
    return best_id if best >= 2 else None


def classify(text: str) -> str | None:
    """Label an existing document as "dilekce" or "sozlesme" (or None) from its title/content.

    Reuses the same catalog + signal words as `detect` so the Belgeler listing never drifts from
    drafting. The curated detection wins; a bare signal word (e.g. "sözleşme") is the fallback.
    """
    tid = detect(text)
    if tid:
        info = get(tid)
        if info:
            return info["tur"]
    t = _fold(text or "")
    if any(sig in t for sig in _SIG_SOZLESME):
        return "sozlesme"
    if any(sig in t for sig in _SIG_DILEKCE):
        return "dilekce"
    return None


def _sections(kind: str, fallback: dict[str, str]) -> str:
    lines: list[str] = []
    for b in _base(kind).get("bolumler", []):
        etiket = b.get("etiket") or fallback.get(b.get("id", ""), b.get("id", "").upper())
        flag = ""
        if b.get("zorunlu"):
            hmk = b.get("hmk", "")
            critical = any(p.strip() in _PET_CRITICAL for p in str(hmk).split(","))
            kritik = " — KRİTİK, boş bırakma" if critical else ""
            flag = f"  [ZORUNLU{(' ' + hmk) if hmk else ''}{kritik}]"
        note = f"  ({b['not']})" if b.get("not") else ""
        lines.append(f"- {etiket}{flag}{note}")
    return "\n".join(lines)


def draft_prompt(type_id: str) -> str | None:
    """Build a format-aware drafting instruction for a type (dilekçe or sözleşme), or None."""
    info = get(type_id)
    if info is None:
        return None

    if info["tur"] == "sozlesme":
        sections = _sections("sozlesme", {"baslik": "BAŞLIK", "imza": "İMZALAR"})
        return (
            f"'{info['ad']}' başlıklı sözleşmeyi hazırla. Dayanak mevzuat: {info['ref']}.\n"
            "Türk sözleşme formatına uy. Madde dizilimi:\n"
            f"{sections}\n"
            "Başlığı ORTALI ve büyük harf yaz. MADDE 1'de tarafları tam unvan + TCKN/VKN + "
            "adres ile "
            "yazıp kısa tanım ata (ör. \"... bundan böyle 'KİRAYA VEREN' olarak anılacaktır\"). "
            "Maddeleri numaralandır (MADDE 1, 2, 3…; alt bentler 4.1, 4.2). Bedeli rakam+yazı "
            "ve KDV "
            "ile, fesih bildirim süresini, yetkili mahkeme/icra dairelerini, uygulanacak "
            "hukuku (Türk "
            "hukuku) ve nüsha adedini açıkça belirt. Bilgi eksikse köşeli parantezle yer "
            "tutucu bırak "
            "(ör. [Kiracı adı]). Geçerlilik için noter/tapu gibi şekil şartı gerekiyorsa sözleşme "
            "sonunda kısa bir not olarak uyar."
        )

    sections = _sections("dilekce", {"baslik": "MAHKEME BAŞLIĞI", "imza": "TARİH / İMZA"})
    return (
        f"'{info['ad']}' hazırla. Görevli/yetkili merci: {info['ref']}.\n"
        "Türk hukuku dilekçe formatına BİREBİR uy. Bölüm dizilimi ve etiketler:\n"
        f"{sections}\n"
        "Mahkeme başlığını büyük harf + datif + kesme ile yaz (ör. "
        '"... NÖBETÇİ ASLİYE HUKUK MAHKEMESİ\'NE"). KRİTİK işaretli bölümler (HMK m.119/a,d,e,f,g) '
        "asla boş kalmamalı; bilgi eksikse köşeli parantezle yer tutucu bırak (ör. [Davalı adı]). "
        "AÇIKLAMALAR'da vakıaları sıra numarasıyla (1., 2., 3.) yaz; her vakıaya delil bağla. "
        "SONUÇ VE İSTEM'de talepleri numaralandır ve yargılama gideri + vekâlet ücreti "
        "talebini ekle."
    )

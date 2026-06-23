"""One-time: generate polished golden example .md per document type (few-shot for drafting).

Works for both kinds — petitions (dilekçe) and contracts (sözleşme):
    ANTHROPIC_MODEL=claude-sonnet-4-6 python scripts/gen_examples.py --kind sozlesme [id ...] [--force]
    ANTHROPIC_MODEL=claude-sonnet-4-6 python scripts/gen_examples.py --kind dilekce  [id ...] [--force]

These are STATIC assets (few-shot exemplars) so the cheap runtime model mimics a professional
layout; runtime drafting stays on whatever ANTHROPIC_MODEL the app uses. Output:
templates/<petition|contract>/examples/<id>.md. Existing files skipped unless --force.
Templates are format skeletons with [köşeli parantez] placeholders — no fabricated facts.
"""

from __future__ import annotations

import pathlib
import sys
import time

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))

from core.env import load_dotenv

load_dotenv()

from core.providers.claude import ClaudeProvider  # noqa: E402
from web import petitions  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parent.parent
_DIR = {"dilekce": "petition", "sozlesme": "contract"}

_SYS = {
    "dilekce": (
        "Sen Türkiye'de kıdemli, titiz bir avukatsın. Görevin, belirtilen dilekçe tipi için gerçek "
        "bir avukatın hazırlayacağı kalitede, EKSİKSİZ ve PROFESYONEL bir MARKDOWN ŞABLON üretmek. "
        "Gerçek kişi/olay/karar UYDURMA; somut bilgiler için [köşeli parantez] yer tutucu kullan. "
        "Mevzuat atıflarını (HMK, TMK, TBK, İİK, İYUK, TCK, CMK vb.) bu dava tipine uygun, DOĞRU ve "
        "yerinde ver. ÇIKTI SADECE markdown belge olsun — açıklama/önsöz YOK, kod bloğu (```) YOK."
    ),
    "sozlesme": (
        "Sen Türkiye'de kıdemli, titiz bir sözleşmeler avukatısın. Görevin, belirtilen sözleşme "
        "tipi için gerçek bir avukatın hazırlayacağı kalitede, EKSİKSİZ ve PROFESYONEL bir MARKDOWN "
        "ŞABLON üretmek. Gerçek kişi/şirket UYDURMA; somut bilgiler için [köşeli parantez] yer "
        "tutucu kullan. Mevzuat atıflarını (TBK, TTK, İş K., KVKK vb.) bu sözleşme tipine uygun, "
        "DOĞRU ver. ÇIKTI SADECE markdown belge olsun — açıklama/önsöz YOK, kod bloğu (```) YOK."
    ),
}


def _user_prompt(kind: str, info: dict, format_md: str) -> str:
    if kind == "sozlesme":
        return (
            f"Sözleşme tipi: {info['ad']}\nDayanak mevzuat: {info['ref']}\n"
            f"Kategori: {info['kategori']}\n\n"
            "Aşağıdaki FORMAT ŞARTNAMESİNE BİREBİR uy:\n\n"
            f"{format_md}\n\n"
            "Eksiksiz bir sözleşme şablonu üret: başlık ORTALI + BÜYÜK HARF; MADDE 1'de tarafları "
            "tam unvan + TCKN/VKN + adres ile yazıp kısa tanım ata (ör. \"... 'KİRAYA VEREN' olarak "
            "anılacaktır\"); maddeleri numaralandır (MADDE 1, 2, 3…; alt bentler 4.1, 4.2); bedeli "
            "rakam+yazı ve KDV ile; fesih/bildirim sürelerini, yetkili mahkeme/icra dairelerini, "
            "uygulanacak hukuku (Türk hukuku), mücbir sebep, gizlilik, tebligat ve nüsha adedini "
            "açıkça belirt. Tüm somut veriler [köşeli parantez] olsun; şekil şartı (noter/tapu) "
            "gerekiyorsa sonda kısa not olarak uyar."
        )
    return (
        f"Dilekçe tipi: {info['ad']}\nGörevli/yetkili merci: {info['ref']}\n"
        f"Kategori: {info['kategori']}\n\n"
        "Aşağıdaki FORMAT ŞARTNAMESİNE BİREBİR uy (bölüm dizilimi, etiketler, başlık biçimi):\n\n"
        f"{format_md}\n\n"
        "Eksiksiz bir şablon üret: mahkeme başlığı (BÜYÜK HARF + datif + kesme), DAVACI/DAVALI (ve "
        "VEKİLİ), KONU, AÇIKLAMALAR (vakıaları 1., 2., 3. ile gerçekçi iskelet), HUKUKİ NEDENLER "
        "(bu tipe ait DOĞRU kanun maddeleri), DELİLLER, SONUÇ VE İSTEM (numaralı + yargılama gideri "
        "ve vekâlet ücreti), tarih ve imza bloğu. Tüm somut veriler [köşeli parantez] olsun."
    )


def _strip_fences(t: str) -> str:
    t = (t or "").strip()
    if t.startswith("```"):
        lines = t.split("\n")[1:]
        if lines and lines[-1].strip().startswith("```"):
            lines = lines[:-1]
        t = "\n".join(lines).strip()
    return t


def main(argv: list[str]) -> int:
    kind = "dilekce"
    if "--kind" in argv:
        kind = argv[argv.index("--kind") + 1]
    force = "--force" in argv
    only = [a for a in argv if not a.startswith("--") and a != kind]

    if kind not in _DIR:
        print(f"geçersiz --kind: {kind} (dilekce|sozlesme)")
        return 1

    ex_dir = ROOT / "templates" / _DIR[kind] / "examples"
    ex_dir.mkdir(parents=True, exist_ok=True)
    format_md = (ROOT / "templates" / _DIR[kind] / "FORMAT.md").read_text(encoding="utf-8")
    cats = petitions.contract_categories() if kind == "sozlesme" else petitions.categories()
    types = [(d["id"], petitions.get(d["id"])) for cat in cats for d in cat["dilekceler"]]
    if only:
        types = [(t, i) for t, i in types if t in only]

    provider = ClaudeProvider()
    done = 0
    for tid, info in types:
        out = ex_dir / f"{tid}.md"
        if out.exists() and not force:
            print(f"[skip] {tid}")
            continue
        try:
            res = provider.step(
                [
                    {"role": "system", "content": _SYS[kind]},
                    {"role": "user", "content": _user_prompt(kind, info, format_md)},
                ],
                [],
            )
            md = _strip_fences(res.text)
            if md:
                out.write_text(md + "\n", encoding="utf-8")
                done += 1
                print(f"[ok] {tid} ({len(md)} ch)")
            else:
                print(f"[empty] {tid}")
        except Exception as e:  # noqa: BLE001
            print(f"[err] {tid}: {e}")
        time.sleep(1.0)

    print(f"--- {done}/{len(types)} üretildi → {ex_dir} ---")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

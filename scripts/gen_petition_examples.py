"""One-time: generate a polished golden example .md per petition type (few-shot for drafting).

These are STATIC assets used as few-shot exemplars so the (cheap) runtime model mimics a
professional layout. Generate them once with a strong model — runtime drafting stays on whatever
ANTHROPIC_MODEL the app uses:

    ANTHROPIC_MODEL=claude-sonnet-4-6 python scripts/gen_petition_examples.py [type_id] [--force]

Output: templates/petition/examples/<id>.md. Existing files are skipped unless --force.
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
EX_DIR = ROOT / "templates" / "petition" / "examples"
FORMAT_MD = (ROOT / "templates" / "petition" / "FORMAT.md").read_text(encoding="utf-8")

SYS = (
    "Sen Türkiye'de kıdemli, titiz bir avukatsın. Görevin, belirtilen dilekçe tipi için gerçek "
    "bir avukatın hazırlayacağı kalitede, EKSİKSİZ ve PROFESYONEL bir MARKDOWN ŞABLON üretmek. "
    "Gerçek kişi/olay/karar UYDURMA; somut bilgiler için [köşeli parantez] yer tutucu kullan. "
    "Mevzuat atıflarını (HMK, TMK, TBK, İİK, İYUK, TCK, CMK vb. ilgili maddeleri) bu dava tipine "
    "uygun, DOĞRU ve yerinde ver. ÇIKTI SADECE belgenin markdown'ı olsun — açıklama, önsöz YOK, "
    "kod bloğu işareti (```) YOK."
)


def _strip_fences(t: str) -> str:
    t = (t or "").strip()
    if t.startswith("```"):
        lines = t.split("\n")[1:]
        if lines and lines[-1].strip().startswith("```"):
            lines = lines[:-1]
        t = "\n".join(lines).strip()
    return t


def _user_prompt(info: dict) -> str:
    return (
        f"Dilekçe tipi: {info['ad']}\n"
        f"Görevli/yetkili merci: {info['ref']}\n"
        f"Kategori: {info['kategori']}\n\n"
        "Aşağıdaki FORMAT ŞARTNAMESİNE BİREBİR uy (bölüm dizilimi, etiketler, başlık biçimi):\n\n"
        f"{FORMAT_MD}\n\n"
        "Bu tip için eksiksiz bir şablon üret: mahkeme başlığı (BÜYÜK HARF + datif + kesme), "
        "DAVACI/DAVALI (ve VEKİLİ), KONU, AÇIKLAMALAR (vakıaları 1., 2., 3. ile bu dava tipine "
        "uygun gerçekçi bir iskelet hâlinde), HUKUKİ NEDENLER (bu tipe ait DOĞRU kanun maddeleri), "
        "DELİLLER, SONUÇ VE İSTEM (numaralı talepler + yargılama gideri ve vekâlet ücreti), tarih "
        "ve imza bloğu. Tüm somut veriler [köşeli parantez] yer tutucu olsun."
    )


def main(argv: list[str]) -> int:
    force = "--force" in argv
    only = [a for a in argv if not a.startswith("--")]
    EX_DIR.mkdir(parents=True, exist_ok=True)
    provider = ClaudeProvider()

    types = [
        (d["id"], petitions.get(d["id"]))
        for cat in petitions.categories()
        for d in cat["dilekceler"]
    ]
    if only:
        types = [(t, i) for t, i in types if t in only]

    done = 0
    for tid, info in types:
        out = EX_DIR / f"{tid}.md"
        if out.exists() and not force:
            print(f"[skip] {tid}")
            continue
        try:
            res = provider.step(
                [
                    {"role": "system", "content": SYS},
                    {"role": "user", "content": _user_prompt(info)},
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
        except Exception as e:
            print(f"[err] {tid}: {e}")
        time.sleep(1.0)

    print(f"--- {done}/{len(types)} üretildi → {EX_DIR} ---")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))

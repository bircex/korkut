"""Evaluation spike: how much does a local NER add to redaction recall? (issue #21)

This is NOT part of the app and NOT a pytest test — spaCy is not a project dependency. It is a
standalone evaluation, run by hand, that answers the build-vs-adopt question for a local-NER
"Layer 3": prototype a NER detector, measure it against the same gold corpus as measure.py, and
compare base vs. base+NER on recall (missed PII) and precision (over-masking).

Setup (one-off, outside the app's deps):

    python -m pip install spacy
    python -m spacy download xx_ent_wiki_sm      # ~11 MB multilingual PER/LOC/ORG model
    python tests/redaction_eval/ner_eval.py      # from the backend/ dir

The model is multilingual (works on Turkish) and cheap to load. PER/LOC/ORG entities become mask
candidates; a small legal allowlist drops court/statute false positives (Yargıtay, "... Hukuku").
"""

from __future__ import annotations

import sys
from pathlib import Path

import yaml

_SRC = Path(__file__).resolve().parents[2] / "src"
if _SRC.is_dir() and str(_SRC) not in sys.path:
    sys.path.insert(0, str(_SRC))

from korkut.core.redactor import Redactor  # noqa: E402  (import follows the sys.path shim)

CORPUS = Path(__file__).with_name("corpus.yaml")

# NER entity types worth masking as PII. MISC/other are ignored (too noisy).
_PII_LABELS = {"PER", "LOC", "ORG"}

# Legal terms/institutions the NER mislabels as PER/ORG/LOC — never PII, so never mask.
_LEGAL_ALLOW = (
    "yargıtay",
    "danıştay",
    "anayasa",
    "sayıştay",
    "mahkeme",
    "hukuk",
    "kanun",
    "daire",
    "kurul",
    "rekabet",
    "cumhuriyet",
    "bölge adliye",
    "icra",
    "noter",
)


def _allowlisted(text: str) -> bool:
    low = text.casefold()
    return any(term in low for term in _LEGAL_ALLOW)


def _pii_entities(nlp, text: str) -> list[str]:
    """NER entity strings that are PII candidates (typed PER/LOC/ORG, not legal terms)."""
    return [e.text for e in nlp(text).ents if e.label_ in _PII_LABELS and not _allowlisted(e.text)]


def _covered(value: str, ents: list[str]) -> bool:
    """Would masking these NER entities remove `value`? (value sits inside a detected entity)."""
    return any(value in e for e in ents)


def main() -> None:
    try:
        import spacy
    except ModuleNotFoundError:
        print("spaCy not installed. See the module docstring for the one-off setup.")
        return
    try:
        nlp = spacy.load("xx_ent_wiki_sm")
    except OSError:
        print("Model missing: python -m spacy download xx_ent_wiki_sm")
        return

    cases = yaml.safe_load(CORPUS.read_text(encoding="utf-8"))["cases"]
    base = Redactor()

    base_hit = base_tot = base_kept = base_ptot = 0
    ner_hit = ner_tot = ner_kept = ner_ptot = 0
    new_catches: list[str] = []
    new_false_pos: list[str] = []

    for case in cases:
        text = case["text"]
        base_out, _ = base.redact(text)
        ents = _pii_entities(nlp, text)

        for value in case.get("must_mask", []):
            base_ok = value not in base_out
            ner_ok = base_ok or _covered(value, ents)
            base_tot += 1
            ner_tot += 1
            base_hit += base_ok
            ner_hit += ner_ok
            if ner_ok and not base_ok:
                new_catches.append(f"{case['id']}: {value!r}")

        for value in case.get("must_keep", []):
            base_ok = value in base_out
            ner_ok = base_ok and not _covered(value, ents)
            base_ptot += 1
            ner_ptot += 1
            base_kept += base_ok
            ner_kept += ner_ok
            if base_ok and not ner_ok:
                new_false_pos.append(f"{case['id']}: {value!r}")

    def pct(a: int, b: int) -> str:
        return f"{(a / b if b else 1.0):6.1%} ({a}/{b})"

    print("=" * 64)
    print("NER EVAL — base vs. base + local NER (xx_ent_wiki_sm)")
    print("=" * 64)
    print(f"Recall    base: {pct(base_hit, base_tot)}   +NER: {pct(ner_hit, ner_tot)}")
    print(f"Precision base: {pct(base_kept, base_ptot)}   +NER: {pct(ner_kept, ner_ptot)}")
    if new_catches:
        print(f"\nNEW catches from NER ({len(new_catches)}):")
        for c in new_catches:
            print(f"  {c}")
    if new_false_pos:
        print(f"\nNEW false positives from NER ({len(new_false_pos)}):")
        for c in new_false_pos:
            print(f"  {c}")
    print("=" * 64)


if __name__ == "__main__":
    main()

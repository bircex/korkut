"""Redaction — pseudonymize PII before any cloud call, restore it on the response.

The single privacy chokepoint (ARCHITECTURE.md §5). Text bound for a *local* model is
never redacted (it does not leave the machine); text bound for the *cloud* always is.

Detectors are driven by config/redaction-rules.yaml (regex + a person-name detector).
TCKN additionally passes the official checksum. Each distinct detected value is replaced
with a stable token like ``[TCKN_1]``; the mapping (token → original) is returned so the
caller can restore the real values in the model's response. The mapping lives in RAM only.

Guiding rule: when unsure, redact (favor over-redaction of cloud-bound client context).
"""

from __future__ import annotations

import re
from pathlib import Path

import yaml

_CONFIG = Path(__file__).resolve().parent.parent / "config" / "redaction-rules.yaml"

# Seed of common Turkish given names; a name immediately followed by 1-2 capitalized
# words is treated as a person. Extensible via config (`person.extra_names`); full NER later.
_SEED_NAMES = {
    "ahmet", "mehmet", "mustafa", "ali", "hüseyin", "hasan", "ibrahim", "ismail", "osman",
    "yusuf", "murat", "ömer", "ramazan", "süleyman", "halil", "emre", "kadir", "fatih",
    "cem", "burak", "kemal", "serkan", "tolga", "barış", "deniz", "can", "ozan",
    "ayşe", "fatma", "emine", "hatice", "zeynep", "elif", "meryem", "şerife", "zehra",
    "sultan", "hanife", "merve", "büşra", "esra", "derya", "selin", "ceren", "ebru",
    "gül", "özlem", "pınar", "sevgi", "yağmur", "defne", "bilge",
}

_CAP = r"[A-ZÇĞİÖŞÜ][a-zçğıöşüâî]+"  # a Turkish capitalized word


def _load_rules() -> dict:
    if _CONFIG.exists():
        return yaml.safe_load(_CONFIG.read_text(encoding="utf-8")) or {}
    return {}


def _valid_tckn(value: str) -> bool:
    """Validate a Turkish national ID (TCKN) by its checksum."""
    if len(value) != 11 or not value.isdigit() or value[0] == "0":
        return False
    d = [int(c) for c in value]
    if (sum(d[0:10]) % 10) != d[10]:
        return False
    odd = d[0] + d[2] + d[4] + d[6] + d[8]
    even = d[1] + d[3] + d[5] + d[7]
    return ((odd * 7 - even) % 10) == d[9]


class Redactor:
    def __init__(self, rules: dict | None = None) -> None:
        rules = rules if rules is not None else _load_rules()
        detectors = (rules.get("detectors") or {})
        self._regex: list[tuple[str, re.Pattern[str], bool]] = []
        for kind, spec in detectors.items():
            pattern = spec.get("pattern") if isinstance(spec, dict) else None
            if pattern:
                self._regex.append(
                    (kind.upper(), re.compile(pattern), bool((spec or {}).get("validate_checksum")))
                )
        person_spec = detectors.get("person") or {}
        extra = {n.lower() for n in (person_spec.get("extra_names") or [])}
        self._names = _SEED_NAMES | extra
        self._word_re = re.compile(_CAP)

    def _find_persons(self, text: str) -> list[tuple[int, int, str, str]]:
        """A known first name + up to 2 immediately following capitalized words."""
        words = list(self._word_re.finditer(text))
        spans: list[tuple[int, int, str, str]] = []
        i = 0
        while i < len(words):
            if words[i].group(0).lower() in self._names:
                start, end = words[i].start(), words[i].end()
                j = i + 1
                while j < len(words) and j - i <= 2 and text[end:words[j].start()].strip() == "":
                    end = words[j].end()
                    j += 1
                spans.append((start, end, "PERSON", text[start:end]))
                i = j
            else:
                i += 1
        return spans

    def _find(self, text: str) -> list[tuple[int, int, str, str]]:
        """Return (start, end, kind, value) spans, longest-first, non-overlapping."""
        spans: list[tuple[int, int, str, str]] = []
        for kind, pattern, check in self._regex:
            for m in pattern.finditer(text):
                if check and kind == "TCKN" and not _valid_tckn(m.group(0)):
                    continue
                spans.append((m.start(), m.end(), kind, m.group(0)))
        spans.extend(self._find_persons(text))
        # Longest match first so overlapping detections don't double-replace.
        spans.sort(key=lambda s: (s[0], -(s[1] - s[0])))
        chosen: list[tuple[int, int, str, str]] = []
        last_end = -1
        for start, end, kind, value in spans:
            if start >= last_end:
                chosen.append((start, end, kind, value))
                last_end = end
        return chosen

    def _apply(
        self,
        text: str,
        value_to_token: dict[str, str],
        counters: dict[str, int],
        mapping: dict[str, str],
    ) -> str:
        spans = self._find(text)
        out = text
        # Replace right-to-left to keep earlier offsets valid.
        for start, end, kind, value in sorted(spans, key=lambda s: s[0], reverse=True):
            token = value_to_token.get(value)
            if token is None:
                counters[kind] = counters.get(kind, 0) + 1
                token = f"[{kind}_{counters[kind]}]"
                value_to_token[value] = token
                mapping[token] = value
            out = out[:start] + token + out[end:]
        return out

    def redact(self, text: str) -> tuple[str, dict[str, str]]:
        """Replace PII with stable tokens. Returns (redacted_text, {token: original})."""
        mapping: dict[str, str] = {}
        out = self._apply(text, {}, {}, mapping)
        return out, mapping

    def redact_batch(self, texts: list[str]) -> tuple[list[str], dict[str, str]]:
        """Redact several texts with ONE shared mapping (same value → same token)."""
        value_to_token: dict[str, str] = {}
        counters: dict[str, int] = {}
        mapping: dict[str, str] = {}
        out = [self._apply(t, value_to_token, counters, mapping) for t in texts]
        return out, mapping

    @staticmethod
    def restore(text: str, mapping: dict[str, str]) -> str:
        """Replace tokens with their original values."""
        for token, original in mapping.items():
            text = text.replace(token, original)
        return text


_default = Redactor.__new__(Redactor)  # lazily initialized singleton


def redact(text: str) -> tuple[str, dict[str, str]]:
    global _default
    if not hasattr(_default, "_regex"):
        _default = Redactor()
    return _default.redact(text)


def restore(text: str, mapping: dict[str, str]) -> str:
    return Redactor.restore(text, mapping)

"""Measure the redactor against the gold corpus.

This is an *evaluation*, not a unit test: it quantifies how the current
`core.redactor` performs on realistic legal text so we know where it leaks before
adding new detector layers.

Two metrics, by design weighted differently:

* recall    — fraction of `must_mask` PII values that were actually removed. A miss
              is a privacy leak (PII reaches the cloud), so this is the metric we
              care about most; the target is ~100% (fail-closed).
* precision — fraction of `must_keep` non-PII values that survived untouched.
              Over-masking only costs answer quality, so it is the softer metric.

Run as a report:

    python tests/redaction_eval/measure.py         # from the backend/ dir

or call `evaluate()` from a test to assert a baseline and track known leaks.
"""

from __future__ import annotations

import sys
from dataclasses import dataclass, field
from pathlib import Path

import yaml

# Allow a bare `python tests/redaction_eval/measure.py` from backend/ without an install:
# pytest puts src/ on the path via its `pythonpath` setting, but a direct run does not.
_SRC = Path(__file__).resolve().parents[2] / "src"
if _SRC.is_dir() and str(_SRC) not in sys.path:
    sys.path.insert(0, str(_SRC))

from korkut.core.redactor import Redactor  # noqa: E402  (import follows the sys.path shim)

CORPUS = Path(__file__).with_name("corpus.yaml")


@dataclass
class Result:
    """Aggregated outcome of one evaluation run."""

    masked: int = 0  # must_mask values correctly removed
    leaked: int = 0  # must_mask values still present (privacy leaks)
    kept: int = 0  # must_keep values correctly preserved
    over_masked: int = 0  # must_keep values wrongly removed
    leaks: list[tuple[str, str, str]] = field(default_factory=list)  # (case_id, value, tag)
    false_positives: list[tuple[str, str, str]] = field(default_factory=list)
    recall_by_tag: dict[str, tuple[int, int]] = field(default_factory=dict)  # tag -> (hit, total)

    @property
    def recall(self) -> float:
        total = self.masked + self.leaked
        return self.masked / total if total else 1.0

    @property
    def precision(self) -> float:
        total = self.kept + self.over_masked
        return self.kept / total if total else 1.0


def _primary_tag(tags: list[str]) -> str:
    """The PII-type tag for a case, ignoring the generic 'precision' marker."""
    for tag in tags:
        if tag != "precision":
            return tag
    return "other"


def evaluate(corpus_path: Path = CORPUS) -> Result:
    """Run the redactor over every case and aggregate recall/precision."""
    cases = yaml.safe_load(corpus_path.read_text(encoding="utf-8"))["cases"]
    redactor = Redactor()
    res = Result()

    for case in cases:
        case_id = case["id"]
        tag = _primary_tag(case.get("tags", []))
        redacted, _ = redactor.redact(case["text"])

        for value in case.get("must_mask", []):
            hit, total = res.recall_by_tag.get(tag, (0, 0))
            if value in redacted:  # the raw PII survived → leak
                res.leaked += 1
                res.leaks.append((case_id, value, tag))
                res.recall_by_tag[tag] = (hit, total + 1)
            else:
                res.masked += 1
                res.recall_by_tag[tag] = (hit + 1, total + 1)

        for value in case.get("must_keep", []):
            if value in redacted:
                res.kept += 1
            else:  # a non-PII value was masked away → false positive
                res.over_masked += 1
                res.false_positives.append((case_id, value, tag))

    return res


def format_report(res: Result) -> str:
    """Render a human-readable summary of an evaluation run."""
    lines: list[str] = []
    lines.append("=" * 64)
    lines.append("REDACTION GOLD-SET MEASUREMENT")
    lines.append("=" * 64)
    lines.append(
        f"Recall (PII removed):   {res.recall:6.1%}  ({res.masked}/{res.masked + res.leaked})"
    )
    lines.append(
        f"Precision (kept clean): {res.precision:6.1%}  ({res.kept}/{res.kept + res.over_masked})"
    )
    lines.append("")
    lines.append("Recall by PII type:")
    for tag in sorted(res.recall_by_tag):
        hit, total = res.recall_by_tag[tag]
        rate = hit / total if total else 1.0
        flag = "" if hit == total else "   <-- LEAKS"
        lines.append(f"  {tag:<20} {rate:6.1%}  ({hit}/{total}){flag}")

    if res.leaks:
        lines.append("")
        lines.append(f"LEAKS — PII that reached the output ({len(res.leaks)}):")
        for case_id, value, tag in res.leaks:
            lines.append(f"  [{tag}] {case_id}: {value!r}")

    if res.false_positives:
        lines.append("")
        lines.append(f"FALSE POSITIVES — over-masked non-PII ({len(res.false_positives)}):")
        for case_id, value, tag in res.false_positives:
            lines.append(f"  [{tag}] {case_id}: {value!r}")

    lines.append("=" * 64)
    return "\n".join(lines)


def main() -> None:
    print(format_report(evaluate()))


if __name__ == "__main__":
    main()

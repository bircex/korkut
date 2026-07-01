"""Regression guard over the redaction gold corpus (see tests/redaction_eval/).

The deterministic detectors (TCKN, phone, IBAN, plate, case number) must never silently
break, so they are pinned at 100% recall. Overall recall is pinned to a documented
baseline so it can only improve as we add detector layers — the known leaks (out-of-seed
names, address, e-mail, company, date of birth) are tracked here rather than asserted away.
"""

from redaction_eval.measure import evaluate

# Detectors backed by regex/checksum — these are deterministic and must stay perfect.
DETERMINISTIC_TAGS = {"tckn", "phone", "iban", "plate", "case_no"}

# Current measured recall (2026-06). Raise this as detector layers land; it must never drop.
RECALL_BASELINE = 0.46


def test_deterministic_detectors_have_full_recall():
    res = evaluate()
    for tag in DETERMINISTIC_TAGS:
        hit, total = res.recall_by_tag.get(tag, (0, 0))
        assert hit == total, f"{tag} regressed: only {hit}/{total} masked"


def test_overall_recall_does_not_regress():
    res = evaluate()
    assert res.recall >= RECALL_BASELINE, (
        f"recall dropped to {res.recall:.1%} (baseline {RECALL_BASELINE:.0%})"
    )


def test_known_leak_categories_are_still_documented():
    # These are the gaps the next detector layers (DB-known entities, local NER) will close.
    # Until then they leak by design; assert they are exactly the categories we expect, so a
    # new, undocumented leak category trips the test.
    res = evaluate()
    leaking_tags = {tag for _, _, tag in res.leaks}
    expected = {"person_out_of_seed", "address", "email", "company", "dob"}
    assert leaking_tags == expected, f"unexpected change in leak categories: {leaking_tags}"

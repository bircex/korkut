"""Tests for core.redactor — PII detection, tokenization, round-trip restore."""

from korkut.core.redactor import Redactor, redact, restore

VALID_TCKN = "10000000146"  # passes the official checksum
INVALID_TCKN = "12345678901"  # fails the checksum


def test_redacts_tckn_phone_iban_and_person():
    text = (
        "Müvekkil Ahmet Yılmaz, TCKN 10000000146, tel 05321234567, IBAN TR330006100519786457841326"
    )
    out, mapping = redact(text)
    assert "10000000146" not in out
    assert "05321234567" not in out
    assert "TR330006100519786457841326" not in out
    assert "Ahmet Yılmaz" not in out
    assert "[TCKN_1]" in out and "[PHONE_1]" in out and "[IBAN_1]" in out and "[PERSON_1]" in out
    # mapping carries the real values
    assert mapping["[TCKN_1]"] == "10000000146"
    assert mapping["[PERSON_1]"] == "Ahmet Yılmaz"


def test_round_trip_restore():
    text = "Davacı Mehmet Demir ile görüşüldü; TCKN 10000000146."
    out, mapping = redact(text)
    assert restore(out, mapping) == text


def test_invalid_tckn_not_redacted():
    out, mapping = redact(f"Numara {INVALID_TCKN} geçersiz")
    assert INVALID_TCKN in out
    assert not any(k.startswith("[TCKN") for k in mapping)


def test_court_name_not_misdetected_as_person():
    # "Yargıtay ..." does not start with a known first name → not a PERSON.
    out, _ = redact("Yargıtay Hukuk Genel Kurulu kararı")
    assert "[PERSON" not in out


def test_same_value_gets_one_token():
    out, mapping = redact("Ahmet Yılmaz ve yine Ahmet Yılmaz")
    assert out.count("[PERSON_1]") == 2
    assert sum(1 for k in mapping if k.startswith("[PERSON")) == 1


def test_local_text_helper_is_pure():
    r = Redactor()
    out, mapping = r.redact("no pii here")
    assert out == "no pii here"
    assert mapping == {}


def test_known_entities_mask_values_the_seed_list_misses():
    # An out-of-seed client name and a bar-registry number the base detectors leak,
    # masked deterministically because the office stores them.
    known = [("Oğuzhan Karaca", "CLIENT"), ("34567", "LAWYER_BAR_NO")]
    r = Redactor(known_entities=known)
    out, mapping = r.redact("Av. sicil 34567; müvekkil Oğuzhan Karaca aleyhine takip.")
    assert "Oğuzhan Karaca" not in out
    assert "34567" not in out
    assert mapping["[CLIENT_1]"] == "Oğuzhan Karaca"
    assert mapping["[LAWYER_BAR_NO_1]"] == "34567"


def test_known_entity_masked_at_every_occurrence():
    r = Redactor(known_entities=[("TeknoMarka", "BRAND")])
    out, _ = r.redact("TeknoMarka sahibi, TeknoMarka markası, yine TeknoMarka.")
    assert "TeknoMarka" not in out
    assert out.count("[BRAND_1]") == 3


def test_longer_known_entity_wins_over_substring():
    # "Ahmet Yılmaz" must win over the bare "Ahmet" so no partial value leaks.
    known = [("Ahmet", "FIRST"), ("Ahmet Yılmaz", "CLIENT")]
    out, _ = Redactor(known_entities=known).redact("Müvekkil Ahmet Yılmaz geldi.")
    assert "[CLIENT_1]" in out
    assert "Yılmaz" not in out

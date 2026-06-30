"""Intake schema (templates/intake/<type_id>.json) loads, is well-formed, matches the catalog."""

from __future__ import annotations

import re

import pytest

from korkut.api import petitions

_VALID_TYPES = {"metin", "tarih", "sayi", "evet_hayir", "secim"}
_EXISTING = list((petitions._INTAKE).glob("*.json"))


def test_unknown_type_returns_none():
    assert petitions.intake("b-boyle-bir-tip-yok") is None


@pytest.mark.parametrize("path", _EXISTING, ids=lambda p: p.stem)
def test_intake_file_is_well_formed(path):
    type_id = path.stem
    data = petitions.intake(type_id)
    assert data is not None, f"{type_id} yüklenemedi"

    # id matches the file name and a real catalog type.
    assert data["tip"] == type_id
    assert petitions.get(type_id) is not None, f"{type_id} catalog.json'da yok"
    assert data.get("ad") and isinstance(data.get("dayanak"), list)

    soru_ids = set()
    for q in data["sorular"]:
        assert q["id"] and q["soru"], f"{type_id}: eksik soru alanı"
        assert q["tip"] in _VALID_TYPES, f"{type_id}: geçersiz tip {q['tip']}"
        assert isinstance(q["zorunlu"], bool)
        if q["tip"] == "secim":
            assert q.get("secenekler"), f"{type_id}: secim sorusu seçeneksiz ({q['id']})"
        soru_ids.add(q["id"])

    # Every condition references a known question id and exposes at least one effect.
    for k in data.get("kosullar", []):
        ref = re.split(r"\s*(==|!=|>=|<=|>|<)\s*", k["eger"])[0].strip()
        ref = re.sub(r"^gun_farki\((.*)\)$", r"\1", ref).strip()
        assert ref in soru_ids, f"{type_id}: koşul bilinmeyen soruya bakıyor: {ref}"
        assert any(key in k for key in ("sor", "ekle", "uyari")), f"{type_id}: koşulun etkisi yok"

    assert isinstance(data.get("kontroller", []), list)
    z = data.get("zenginlestir", {})
    assert isinstance(z.get("mevzuat_dogrula", []), list)

    # Thin-intake invariant: evidence/jurisdiction/time-limits are INHERITED from the
    # dava-workflow archetype (see docs/DRAFTING_FLOW.md) and must not be duplicated here.
    forbidden = {"deliller", "gorevli_mahkeme", "yetki", "dava_sarti", "kritik_sureler"}
    dup = forbidden & set(data)
    assert not dup, f"{type_id}: miras alanını tekrarlıyor {dup}"


def test_full_catalog_coverage():
    # Every dilekçe type in the catalog has an intake schema.
    catalog_ids = {d["id"] for c in petitions.categories() for d in c["dilekceler"]}
    have = {p.stem for p in _EXISTING}
    assert not (catalog_ids - have), f"intake eksik: {sorted(catalog_ids - have)}"


@pytest.mark.parametrize(
    ("type_id", "expected"),
    [
        ("cekismeli-bosanma", "bosanma"),
        ("ise-iade", "is-davasi"),
        ("tahliye-temerrut", "kira-tahliye"),
        ("vergi-iptal", "vergi-davasi"),
        ("ilamsiz-takip-talebi", "icra-takibi"),
    ],
)
def test_dava_turu_resolves_via_category(type_id, expected):
    turu = petitions.dava_turu(type_id)
    assert turu is not None and turu["id"] == expected


def test_dava_turu_null_category_returns_none():
    # Kanun Yolları / AYM categories inherit nothing (null in kategori_eslesme).
    assert petitions.dava_turu("istinaf-basvuru") is None
    assert petitions.dava_turu("aym-bireysel-basvuru") is None


def test_draft_prompt_injects_inherited_and_intake():
    # ise-iade has both an archetype (evidence/yetki) and a type-specific intake file.
    dp = petitions.draft_prompt("ise-iade")
    assert dp and "— OLAYA ÖZGÜ YÖNERGE —" in dp
    assert "YETKİ:" in dp and "DELİLLER" in dp  # inherited from dava-workflow
    assert "USUL KONTROL" in dp  # from the intake file


def test_required_hint_gate():
    # The completeness gate lists must-have facts and tells Korkut to ask before drafting.
    h = petitions.required_hint("cekismeli-bosanma")
    assert h and "BELGE HAZIRLIK" in h and "dilekce_canvas" in h
    assert petitions.required_hint("b-boyle-bir-tip-yok") is None


def test_inheritance_examples_present():
    # The "miras" pilot ships with two intake schemas.
    assert petitions.intake("veraset-ilami") is not None
    assert petitions.intake("mirasin-reddi") is not None

"""Model catalog resolution (`office_model_cfg` via the registry) + per-request token accounting."""

from __future__ import annotations

import contextvars
import importlib

from korkut.core import usage

# ---- token accounting (contextvars accumulator) ----


def test_usage_accumulates_across_calls():
    usage.reset()
    usage.add(10, 5)
    usage.add(3, 7)
    assert usage.snapshot() == {"prompt": 13, "completion": 12}


def test_usage_is_noop_and_zero_without_a_window():
    # A context that never called reset() must not crash and must report zeros.
    ctx = contextvars.Context()
    ctx.run(lambda: usage.add(5, 5))
    assert ctx.run(usage.snapshot) == {"prompt": 0, "completion": 0}


def test_usage_windows_are_isolated_per_context():
    usage.reset()
    usage.add(1, 1)
    # A separate context has its own (absent) window — it does not see our totals.
    other = contextvars.Context()
    assert other.run(usage.snapshot) == {"prompt": 0, "completion": 0}
    assert usage.snapshot() == {"prompt": 1, "completion": 1}


# ---- registry-backed office model resolution ----


def _fresh_store(tmp_path, monkeypatch):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    store_mod.engine()  # build the schema (create_all) for this fresh sqlite file
    return store_mod


def _seed(store, *, enabled: bool, base_url: str = "https://api.deepseek.com/v1"):
    from korkut.api.models import Office, OfficeModel, RegisteredModel

    with store.session() as s:
        s.add(Office(id=1, name="A"))
        s.flush()
        reg = RegisteredModel(
            label="DeepSeek", model="deepseek-chat", base_url=base_url, api_key="sk-x"
        )
        s.add(reg)
        s.flush()
        s.add(OfficeModel(enabled=enabled, model_id=reg.id))
        s.commit()
        return reg.id


def test_office_model_cfg_resolves_assigned_model(tmp_path, monkeypatch):
    from korkut.core.tenancy import reset_current_office, set_current_office

    store = _fresh_store(tmp_path, monkeypatch)
    tok = set_current_office(1)
    try:
        _seed(store, enabled=True)
        with store.session() as s:
            cfg = store.office_model_cfg(s)
        assert cfg is not None
        assert cfg.model == "deepseek-chat"
        assert cfg.base_url == "https://api.deepseek.com/v1"
        assert cfg.api_key == "sk-x"
        assert cfg.local is False
    finally:
        reset_current_office(tok)


def test_office_model_cfg_none_when_disabled(tmp_path, monkeypatch):
    from korkut.core.tenancy import reset_current_office, set_current_office

    store = _fresh_store(tmp_path, monkeypatch)
    tok = set_current_office(1)
    try:
        _seed(store, enabled=False)
        with store.session() as s:
            assert store.office_model_cfg(s) is None
    finally:
        reset_current_office(tok)


def test_office_model_cfg_none_when_unassigned(tmp_path, monkeypatch):
    from korkut.api.models import Office, OfficeModel
    from korkut.core.tenancy import reset_current_office, set_current_office

    store = _fresh_store(tmp_path, monkeypatch)
    tok = set_current_office(1)
    try:
        with store.session() as s:
            s.add(Office(id=1, name="A"))
            s.flush()
            s.add(OfficeModel(enabled=True, model_id=None))  # enabled but nothing assigned
            s.commit()
        with store.session() as s:
            assert store.office_model_cfg(s) is None
    finally:
        reset_current_office(tok)

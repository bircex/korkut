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


def test_admin_notify_addresses_parses_csv(monkeypatch):
    from korkut.api import store

    monkeypatch.setenv("KORKUT_ADMIN_NOTIFY_EMAIL", " a@x.co , b@y.co ,")
    assert store.admin_notify_addresses() == ["a@x.co", "b@y.co"]
    monkeypatch.delenv("KORKUT_ADMIN_NOTIFY_EMAIL", raising=False)
    assert store.admin_notify_addresses() == []


def test_send_simple_email_without_smtp_returns_failed(monkeypatch):
    from korkut.api import store

    for k in ("KORKUT_SMTP_FROM", "KORKUT_SMTP_USER", "KORKUT_SMTP_PASSWORD"):
        monkeypatch.delenv(k, raising=False)
    r = store.send_simple_email("x@y.co", "s", "b")
    assert not r.ok and r.detail


def test_send_simple_email_sends_when_configured(monkeypatch):
    from korkut.api import store
    from korkut.messaging import SendResult

    monkeypatch.setenv("KORKUT_SMTP_FROM", "ops@korkut.co")
    monkeypatch.setenv("KORKUT_SMTP_PASSWORD", "pw")
    captured = {}

    class FakeChannel:
        def send(self, draft):
            captured["draft"] = draft
            return SendResult(ok=True)

    monkeypatch.setattr(store, "build_channel", lambda kind, cfg: FakeChannel())
    r = store.send_simple_email("client@x.co", "Konu", "Gövde")
    assert r.ok
    assert captured["draft"].to_addr == "client@x.co"
    assert captured["draft"].subject == "Konu"


def test_send_simple_email_swallows_send_errors(monkeypatch):
    from korkut.api import store

    monkeypatch.setenv("KORKUT_SMTP_FROM", "ops@korkut.co")
    monkeypatch.setenv("KORKUT_SMTP_PASSWORD", "pw")

    def boom(kind, cfg):
        raise OSError("smtp down")

    monkeypatch.setattr(store, "build_channel", boom)
    r = store.send_simple_email("x@y.co", "s", "b")
    assert not r.ok and "smtp down" in r.detail


def test_claude_provider_records_usage():
    # The built-in default (Claude) path must feed the same accumulator as the OpenAI-compat path.
    from types import SimpleNamespace

    from korkut.core.providers.claude import ClaudeProvider

    class FakeMessages:
        def create(self, **kwargs):
            return SimpleNamespace(
                stop_reason="end_turn",
                content=[SimpleNamespace(type="text", text="merhaba")],
                usage=SimpleNamespace(input_tokens=42, output_tokens=8),
            )

    fake_client = SimpleNamespace(messages=FakeMessages())
    provider = ClaudeProvider(client=fake_client)

    usage.reset()
    result = provider.step([{"role": "user", "content": "selam"}], [])
    assert result.text == "merhaba"
    assert usage.snapshot() == {"prompt": 42, "completion": 8}


def test_gpt_provider_records_usage():
    # The OpenAI-compatible path (registry models: DeepSeek/Groq/OpenAI) feeds the accumulator too.
    from types import SimpleNamespace

    from korkut.core.providers.gpt import GptProvider

    message = SimpleNamespace(content="selam", tool_calls=None)
    resp = SimpleNamespace(
        choices=[SimpleNamespace(message=message)],
        usage=SimpleNamespace(prompt_tokens=30, completion_tokens=12),
    )

    class FakeCompletions:
        def create(self, **kwargs):
            return resp

    fake_client = SimpleNamespace(chat=SimpleNamespace(completions=FakeCompletions()))
    provider = GptProvider(client=fake_client)

    usage.reset()
    result = provider.step([{"role": "user", "content": "selam"}], [])
    assert result.text == "selam"
    assert usage.snapshot() == {"prompt": 30, "completion": 12}


def test_gpt_provider_stream_records_usage_and_streams_tokens():
    # Streaming path: text deltas call on_token; the trailing usage-only chunk is counted.
    from types import SimpleNamespace

    from korkut.core.providers.gpt import GptProvider

    def _delta(text):
        return SimpleNamespace(
            choices=[SimpleNamespace(delta=SimpleNamespace(content=text, tool_calls=None))],
            usage=None,
        )

    chunks = [
        _delta("Mer"),
        _delta("haba"),
        # trailing usage-only chunk: no choices, carries usage
        SimpleNamespace(choices=[], usage=SimpleNamespace(prompt_tokens=5, completion_tokens=3)),
    ]

    class FakeCompletions:
        def create(self, **kwargs):
            assert kwargs["stream"] is True
            assert kwargs["stream_options"] == {"include_usage": True}
            return iter(chunks)

    fake_client = SimpleNamespace(chat=SimpleNamespace(completions=FakeCompletions()))
    provider = GptProvider(client=fake_client)

    seen: list[str] = []
    usage.reset()
    result = provider.step_stream([{"role": "user", "content": "selam"}], [], on_token=seen.append)
    assert "".join(seen) == "Merhaba"
    assert result.text == "Merhaba"
    assert usage.snapshot() == {"prompt": 5, "completion": 3}


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

"""Phase 2 regression: intent is model-driven, not a keyword gate.

The old chat turn ran `petitions.detect()` on every message and injected a per-type intake hint
(`required_hint`) as `extra_system`, which mis-fired on plain questions ("boşanma davası nasıl
açılır?" → petition intake interrogation). That injection is gone; the drafting/intake decision now
lives in the dilekce_canvas tool contract and the model decides. This test pins that the chat
endpoint no longer passes `extra_system` to the brain.
"""

from unittest.mock import MagicMock, patch

import pytest
from fastapi.testclient import TestClient


@pytest.fixture
def client_and_spy(tmp_path, monkeypatch):
    monkeypatch.setenv("KORKUT_DB_URL", f"sqlite+pysqlite:///{tmp_path / 't.db'}")
    monkeypatch.setenv("KORKUT_SECRET", "test-secret-stable")
    import importlib

    import korkut.api.auth as auth_mod
    import korkut.api.store as store_mod

    importlib.reload(store_mod)
    auth_mod._secret_cache = None
    import korkut.api.app as app_mod

    importlib.reload(app_mod)
    import korkut.api.adduser as adduser

    adduser.main(["avukat", "--password", "parola123", "--name", "Av. Test"])
    spy = MagicMock(return_value="yanıt")
    with patch.object(app_mod.agent, "answer", spy):
        c = TestClient(app_mod.app)
        # 2FA is off by default, so the password alone yields a full session.
        c.post("/api/login", json={"username": "avukat", "password": "parola123"})
        yield c, spy


@pytest.mark.parametrize(
    "text",
    [
        "Boşanma davası nasıl açılır?",  # topic-looking QUESTION — must not be pre-gated
        "İşçi kıdem tazminatı nasıl hesaplanır?",
        "Bana bir boşanma dilekçesi yaz",  # even a real draft request: no keyword pre-injection
    ],
)
def test_chat_never_injects_intake_gate(client_and_spy, text):
    c, spy = client_and_spy
    sid = c.post("/api/sessions").json()["id"]
    assert c.post(f"/api/sessions/{sid}/chat", json={"text": text}).status_code == 200
    assert spy.called
    _, kwargs = spy.call_args
    assert kwargs.get("extra_system") is None  # the keyword-triggered gate is gone

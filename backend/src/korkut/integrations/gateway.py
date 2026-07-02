"""The gateway — bridge between an inbound channel message and Korkut's brain.

This is the one place a Telegram/WhatsApp/e-mail message becomes a Korkut answer. It does exactly
what the web chat endpoint does, minus the browser session: resolve the sender to a `User`,
attach the message to the office `Conversation`, run the *same* `agent.answer` loop (so cloud
traffic still goes through the router's redaction and the office's own model config), persist the
turn, and hand the reply text back to the connector to deliver.

Sessions are opened around the model call, never held across it (the model call is network I/O),
mirroring `api.app`.
"""

from __future__ import annotations

from datetime import UTC, datetime

from sqlalchemy import select
from sqlalchemy.orm import Session

from korkut.api import matters_store, store
from korkut.api.models import Conversation, IntegrationThread, Message, User
from korkut.brain import resilience
from korkut.integrations import identity
from korkut.integrations.base import InboundMessage

_HISTORY_TURNS = 20

# Shown when someone messages the bot without a linked account. Kept short and instructive; it is
# the only thing an unlinked sender can ever get out of the gateway.
_UNLINKED = (
    "Bu numara/hesap henüz bir Korkut kullanıcısına bağlı değil. Korkut web arayüzünde "
    "Ayarlar → Entegrasyonlar bölümünden bir bağlanma kodu alın ve buraya yazın "
    "(örn. “BAGLAN ABC123”)."
)


def _utcnow() -> datetime:
    return datetime.now(UTC).replace(tzinfo=None)


def _office_name(s: Session) -> str | None:
    office = store.get_office(s)
    return office.name if office else None


def _conversation_for(s: Session, inbound: InboundMessage, user: User) -> Conversation:
    """Find or create the office `Conversation` backing this channel thread."""
    link = s.scalar(
        select(IntegrationThread).where(
            IntegrationThread.channel == inbound.channel,
            IntegrationThread.thread_id == inbound.thread_id,
        )
    )
    if link is not None:
        conv = s.get(Conversation, link.conversation_id)
        if conv is not None:
            return conv
    label = inbound.sender_name or inbound.subject or inbound.thread_id
    title = f"[{inbound.channel}] {label}"[:120]
    conv = Conversation(
        title=title,
        created_by=user.id,
        created_by_name=user.full_name,
    )
    s.add(conv)
    s.flush()  # need conv.id for the thread row
    s.add(
        IntegrationThread(
            channel=inbound.channel,
            thread_id=inbound.thread_id,
            conversation_id=conv.id,
        )
    )
    return conv


def handle(inbound: InboundMessage, *, on_event=None) -> str | None:
    """Process one inbound message; return the reply text to send back (or None to stay silent).

    `on_event`, when set, receives the brain's progress events (token/tool) so the runner can show
    live activity on the channel (typing + a status line). It never affects the returned reply.
    """
    text = (inbound.text or "").strip()
    if not text:
        return None

    with store.session() as s:
        user = identity.resolve(s, inbound.channel, inbound.external_id)
        if user is None:
            bound = identity.try_bind(s, inbound.channel, inbound.external_id, text)
            if bound is None:
                return _UNLINKED
            name = bound.full_name or bound.username
            return (
                f"Bağlandınız, {name}. Artık Korkut'a buradan yazabilirsiniz. "
                "Nasıl yardımcı olabilirim?"
            )

        # Defence in depth: the resolved user must be active.
        if not user.is_active:
            return None

        conv = _conversation_for(s, inbound, user)
        history = [{"role": m.role, "content": m.content} for m in conv.messages[-_HISTORY_TURNS:]]
        if not conv.messages and conv.title.startswith("["):
            conv.title = (text[:60] + ("…" if len(text) > 60 else "")) or conv.title
        s.add(Message(conversation_id=conv.id, user_id=user.id, role="user", content=text))
        s.commit()
        conv_id = conv.id
        office_name = _office_name(s)
        model_cfg = store.office_model_cfg(s)
        known_entities = matters_store.collect_known_entities(s)
        persona_cfg = store.get_persona(s)
        memory = store.collect_memory(s)

    # A model failure must still reach the sender — on a chat channel an unhandled exception is
    # silence. `guarded_answer` retries transient faults and turns the rest into a friendly
    # Turkish message (the SAME text the web app shows); we send that as the reply. The failure
    # note is not persisted as an assistant turn — it is transport feedback, not part of the case
    # record — so a retry starts clean.
    try:
        reply = resilience.guarded_answer(
            text,
            office_name=office_name,
            history=history,
            model_cfg=model_cfg,
            known_entities=known_entities,
            persona_cfg=persona_cfg,
            memory=memory,
            on_event=on_event,
        )
    except resilience.ModelError as exc:
        return exc.detail

    with store.session() as s:
        conv = s.get(Conversation, conv_id)
        if conv is not None:
            s.add(Message(conversation_id=conv_id, role="assistant", content=reply))
            conv.updated_at = _utcnow()
            s.commit()
    return reply

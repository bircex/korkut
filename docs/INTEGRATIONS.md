# Integrations — two-way chat over Telegram, WhatsApp and e-mail

## Goal

A lawyer can **talk to Korkut from outside the web app** — Telegram, WhatsApp, or e-mail — and get
the same answers, from the same brain, with the same redaction. This is the inbound/two-way
counterpart to [MESSAGING.md](MESSAGING.md) (which only *sends* on the lawyer's behalf).

The whole thing is a thin **gateway** in front of the existing `brain.agent.answer`: a channel
message comes in, we figure out which lawyer sent it, run the normal agent loop, and send the reply
back. New channels are pluggable — the shape is clawbot's (a gateway + per-channel adapters), in
Korkut's own Python stack.

## Decisions

- **Identity binding is mandatory.** Telegram/WhatsApp/e-mail only give us an opaque sender id,
  never Korkut credentials. So a lawyer **links** a channel once (one-time code → `IntegrationLink`),
  and every later message is authenticated by `(channel, external_id)`. No link → the bot only ever
  replies with “how to link”. This preserves Korkut's per-user model (office scoping, per-author
  audit, the office's own model config).
- **One bot per office.** Connector credentials live on `OfficeChannel` (per office, per kind), so
  each office runs its own Telegram bot / WhatsApp number / mailbox. Several lawyers in one office
  share that single bot — they're told apart by their individual links. A leaked token then touches
  only one office.
- **Poll, don't webhook.** Connectors are polled by a runner on a schedule — identical deployment to
  the existing send dispatcher. No public URL or inbound ports.
- **WhatsApp via a Node/Baileys sidecar** (like clawbot). WhatsApp has no bot API; the sidecar owns a
  WhatsApp Web session and exposes a localhost HTTP API the Python connector polls.
- **Reuse, don't fork.** Inbound e-mail (IMAP) sends its replies through the existing `GmailChannel`
  (SMTP); a channel message becomes a normal office `Conversation`, so these chats also show up in
  the web UI.

## Architecture

```
korkut/integrations/
  base.py        Connector ABC: kind, poll() -> [InboundMessage], send(OutboundReply) -> SendResult
  registry.py    CONNECTORS: kind -> Connector ; build_connector(kind, office_id, config)
  identity.py    link a channel sender to a User (one-time code → IntegrationLink); resolve()
  gateway.py     InboundMessage -> resolve user -> agent.answer -> reply (the brain bridge)
  telegram.py    Telegram Bot API connector (httpx long-poll)
  whatsapp.py    WhatsApp connector — HTTP bridge to the Node/Baileys sidecar
  email_imap.py  e-mail connector (IMAP inbound poll + SMTP send via GmailChannel)
  runner.py      `python -m korkut.integrations.runner` — one polling pass over every connector
  sidecar/       the Node/Baileys WhatsApp Web service (separate process)
```

`Connector` is the two-way sibling of `messaging.Channel`: `poll()` fetches messages received since
the last pass, `send()` replies. Each connector keeps its read cursor in `config['_cursor']` and
reports changes via `state_patch()`, which the runner persists onto the `OfficeChannel` row.

## Data model (`api/models.py`)

```
IntegrationLink     (channel sender → korkut User)
  office_id, user_id, channel ("telegram"|"whatsapp"|"email")
  external_id (telegram id / whatsapp jid / e-mail addr), code (one-time, while pending)
  status: pending | active ; UNIQUE(channel, external_id)

IntegrationThread   (channel chat → office Conversation, so history persists + shows in the UI)
  office_id, channel, thread_id, conversation_id ; UNIQUE(channel, thread_id)
```

Connector credentials reuse `OfficeChannel(office_id, kind, enabled, config)` — same table as the
send-only channels. Config keys per kind:

| kind | config |
|------|--------|
| `telegram` | `bot_token` |
| `whatsapp` | `base_url` (sidecar, default `http://127.0.0.1:8099`), `token` (shared secret) |
| `email` | the Gmail SMTP keys (`from_address`, `app_password`, …); IMAP defaults to them, override with `imap_host`/`imap_port`/`imap_user`/`imap_password` |

## Flow

1. **Link (once).** In the web UI a lawyer requests a code → `POST /api/integrations/{kind}/link`
   creates a `pending` `IntegrationLink` and returns a 6-char code. The lawyer sends it from the
   channel (e.g. `BAGLAN ABC123`); `gateway` + `identity.try_bind` capture the sender's
   `external_id` and flip the link to `active`. Codes expire after 15 minutes.
2. **Chat.** The `runner` polls each enabled connector. For every `InboundMessage`, `gateway.handle`
   resolves the sender (`identity.resolve`), finds/creates the office `Conversation` for that thread,
   stores the message, runs `agent.answer` (→ redaction + the office's model), stores the reply, and
   the connector sends it back.
3. **Unlinked senders** get only a short “here's how to link” reply — never an answer.

## API (`api/app.py`)

- `GET /api/integrations` — per-channel status (enabled/configured) + the caller's link state.
- `PUT /api/channels/telegram` · `PUT /api/channels/whatsapp` — owner-only connector config
  (Gmail/e-mail is configured via the existing `PUT /api/channels/email`).
- `POST /api/integrations/{kind}/link` — caller generates a one-time link code.
- `DELETE /api/integrations/{kind}/link` — caller unlinks their account.

## Running it

- **Docker:** the compose `integrations` service loops `python -m korkut.integrations.runner` every
  few seconds. WhatsApp adds the `whatsapp-sidecar` service (profile `whatsapp`) — see below.
- **Non-Docker:** the `korkut.integrations.runner` line in `deploy/cron/korkut.crontab`.

### Per channel

- **Telegram** — create a bot with [@BotFather](https://t.me/BotFather), put the token in
  `PUT /api/channels/telegram`, enable. No phone number needed; the bot is its own identity.
- **WhatsApp** — needs the Node sidecar (`integrations/sidecar/`, see its README): `npm install`,
  set `KORKUT_WA_TOKEN`, start it, scan the QR. Put the same token (and `base_url`) in
  `PUT /api/channels/whatsapp`. WhatsApp has no separate bot identity — the **paired phone number is
  the bot**, so use a dedicated number if you don't want it to act as a personal account.
- **E-mail** — reuse the office's Gmail config (`PUT /api/channels/email`). One Gmail App Password
  covers both SMTP (send) and IMAP (receive); just enable IMAP in the Gmail account.

## Security / guardrails

- **Linked senders only.** An unlinked sender can never get an answer, only the link prompt.
- **Office-scoped binding.** A code only activates within the office whose connector received it; a
  resolved user must belong to that office and be active (checked in the gateway).
- **Same redaction.** Inbound text goes through `agent.answer` → the router's redaction before any
  cloud call, exactly like the web chat.
- **WhatsApp sidecar** binds to localhost and requires a Bearer token; it can send as the paired
  account, so keep the host trusted and the token secret.
- **E-mail `From` is spoofable.** Inbound mail is trusted as the *linked* address, but SMTP `From`
  can be forged. Use a dedicated mailbox for this channel and treat it as lower-trust; a spoofed
  `From` matching a linked address is a known v1 limitation. (Telegram/WhatsApp ids are not
  user-spoofable.)

## Out of scope (v1)

- Attachments / media (text only; images go through the web upload + local OCR path).
- Per-message send confirmation on chat channels (the brain answers conversationally; the
  human-in-the-loop **send_email** confirmation flow stays in the web UI — see MESSAGING.md).
- Group-chat semantics beyond “reply into the chat” (sender is resolved per individual link).

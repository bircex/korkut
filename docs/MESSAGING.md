# Messaging — outbound channels (Gmail now; Telegram/SMTP/Outlook later)

## Goal

Korkut can **send messages on the lawyer's behalf, with the lawyer's confirmation**. The lawyer
says "şu maili gönder"; Korkut proposes it; the lawyer reviews (To / Subject / Body) and clicks
**Gönder** (or schedules it for a date). First channel: **Gmail (send-only, SMTP + App Password)**.
The architecture is **channel-agnostic** so Telegram, generic SMTP and Outlook drop in later — the
long-term vision is a multi-channel legal bot (mail + Telegram, two-way).

Human-in-the-loop is mandatory: the LLM never sends autonomously. Sending is irreversible and
legally sensitive (deadlines, privilege, KVKK) → every send goes through an explicit approval,
mirroring the existing draft→approve (ProposedEdit) pattern.

## Decisions

- **Gmail via SMTP + App Password** (smtp.gmail.com:587, STARTTLS). Send-only, no OAuth/Google
  Cloud project; sent mail lands in the office's Gmail "Sent". Mail leaves from the office's own
  account (data stays with the office).
- **Channel abstraction** (Strategy/Provider pattern, like the LLM `Provider`): one `Channel`
  interface, `GmailChannel` today, more later — registered in a `channel_registry`.
- **Generic scheduled-send queue from day one** (`OutboundMessage`), channel-agnostic: immediate
  AND "X tarihinde gönder" + reminders run on the same table + dispatcher, for every channel.
- **Per-office config** (like `OfficeModel`): each office stores its own channel credentials.

## Architecture

```
korkut/messaging/
  channel.py     Channel ABC: kind, send(OutboundDraft) -> SendResult ; OutboundDraft dataclass
  gmail.py       GmailChannel  (SMTP/STARTTLS via App Password)
  registry.py    CHANNELS: kind -> Channel class ; build_channel(kind, config) -> Channel
  dispatcher.py  dispatch_due(): send queued messages whose send_at <= now (run by cron)
```

`Channel` is provider-independent and unit-testable (the SMTP client is injected, like the LLM
providers take an injected client).

## Data model (`api/models.py`)

```
OfficeChannel   (per-office, per-kind credentials — generic JSON config)
  office_id, kind ("email"), enabled, config (JSON), updated_at
  UNIQUE(office_id, kind)
  # email config: {from_address, from_name, app_password, host=smtp.gmail.com, port=587}

OutboundMessage (the channel-agnostic outbox + schedule queue)
  id, office_id, user_id (author), kind
  to_addr, subject, body            # generic recipient/subject/body
  send_at (nullable → send on confirm; future → scheduled)
  status: draft | queued | sent | failed | cancelled
  error (nullable), attempts, created_at, sent_at
```

## Flow (send_email tool + confirmation)

1. The chat endpoint passes a session-bound `send_email` tool + handler to `agent.answer`
   (exactly like the Canvas signal). Korkut calls it with `{to, subject, body, send_at?}`.
2. The handler writes an `OutboundMessage(status="draft")` and signals the UI; the chat shows a
   **confirmation card** (To / Subject / Body, all editable; optional date for scheduling).
3. Lawyer **Gönder** → `POST /api/outbox/{id}/confirm`:
   - `send_at` empty/past → send now via `build_channel(...).send(...)` → `sent`/`failed`.
   - `send_at` future → `queued`; the dispatcher sends it when due.
   Lawyer **İptal** → `cancelled`.
4. `dispatcher.dispatch_due()` (run by `deploy/cron` every minute) sends `queued` messages whose
   `send_at <= now`. Reminders are just an `OutboundMessage` to the lawyer themselves.

## API (`api/app.py`)

- `GET/PUT /api/channels/email` — per-office Gmail config (owner only; app_password write-only).
- `PUT /api/outbox/{id}` — edit a draft (to/subject/body/send_at) before confirming.
- `POST /api/outbox/{id}/confirm` · `POST /api/outbox/{id}/cancel`.
- `GET /api/outbox` — list (draft/queued/sent/failed) for the "Giden" view.

## UI (`frontend/`)

- **Confirmation card** in chat when Korkut proposes a mail (editable To/Subject/Body + date +
  Gönder/İptal) — reuses the approve pattern.
- **Channel settings** panel (in Ayarlar): Gmail address + App Password + enable.
- **Giden (Outbox)** list: queued/sent/failed with status.

## Security / guardrails

- App Password stored per-office (same Text pattern as `OfficeModel.api_key`; self-hosted DB).
- The LLM only ever produces a **draft**; nothing leaves without the lawyer's explicit confirm.
- Drafting still runs through redaction (cloud-bound); the actual send is direct from the office.
- Recipient is always shown to the lawyer before send (no silent recipients).

## Build phases

1. `korkut/messaging` package (Channel + GmailChannel + registry) + models + config helpers — unit-tested with an injected SMTP fake.
2. `send_email` tool + confirmation endpoints + outbox.
3. Dispatcher (cron) for scheduled sends + reminders.
4. UI (confirmation card, channel settings, outbox).

## Out of scope (v1)

- Inbox reading / two-way threads (send-only for now; Telegram two-way is a later channel).
- Attachments (text body first; attach the drafted document as a follow-up).
- OAuth/Gmail API (SMTP App Password covers send-only cleanly).

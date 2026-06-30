# Korkut WhatsApp sidecar (Node / Baileys)

WhatsApp has no first-class bot API, so — exactly like clawbot — Korkut drives **WhatsApp Web**
through [Baileys](https://github.com/WhiskeySockets/Baileys). This small Node process owns the
WhatsApp session and exposes a localhost HTTP API that the Python `whatsapp` connector polls.

```
GET  /inbox?after=<id>   -> { messages: [{id, from, chat, text, name}], cursor: <lastId> }
POST /send  {to, text}   -> { ok: true }
GET  /status             -> { connected, hasQR }
```

## Run

```bash
cd backend/src/korkut/integrations/sidecar
npm install
KORKUT_WA_TOKEN=$(openssl rand -hex 16) \
KORKUT_WA_AUTH_DIR=../../../../../data/wa-auth \
npm start
```

First start prints a **QR code** in the terminal. On the phone that will host the bot number:
**WhatsApp → Settings → Linked Devices → Link a Device** and scan it. The session is saved in
`KORKUT_WA_AUTH_DIR`, so later restarts reconnect without a new scan.

> The phone's WhatsApp account becomes the bot. Use a **dedicated number** if you don't want the
> bot to act as your personal account (see the architecture note in `docs/INTEGRATIONS.md`).

## Wire it to Korkut

In the office's `whatsapp` channel config (`OfficeChannel.config`, kind `whatsapp`):

```json
{ "base_url": "http://127.0.0.1:8099", "token": "<same KORKUT_WA_TOKEN>" }
```

Enable the channel, then the `integrations` runner will poll `/inbox` and reply via `/send`.

## Env

| var | default | meaning |
|-----|---------|---------|
| `KORKUT_WA_PORT` | `8099` | HTTP port (bind localhost only) |
| `KORKUT_WA_TOKEN` | — (required) | shared Bearer secret; must match the connector config |
| `KORKUT_WA_AUTH_DIR` | `./auth` | where the WhatsApp session is persisted |
| `KORKUT_WA_BUFFER` | `1000` | max buffered inbound messages |

## Security

- The API only accepts `Authorization: Bearer <KORKUT_WA_TOKEN>` and binds to `127.0.0.1`.
- This process can send WhatsApp messages **as the paired account** — keep the host trusted and the
  token secret. Korkut's own auth (identity links + redaction) sits on the Python side.

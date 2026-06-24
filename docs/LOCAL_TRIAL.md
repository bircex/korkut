# Local Trial — run Korkut on your machine

The brain runs on **Claude** (Anthropic) for the trial; switch to OpenAI later by editing
`config/model-routing.yaml` (`backends.cloud: gpt`). The only thing you must provide is an
**Anthropic API key**. The chat UI is our own self-hosted web app — no third-party platform.

## 0. One-time dev setup
```bash
python3 -m venv .venv
.venv/bin/pip install -e "backend/[app,dev]"
cp .env.example .env          # then edit .env (see below)

# Build the SPA (React + Vite) into the API's static dir — needed before `python -m korkut.api`:
npm --prefix frontend install
npm --prefix frontend run build
# During UI work, `npm --prefix frontend run dev` runs Vite on :5173 and proxies /api to :8080.
```

## 1. Quickest check — talk to the brain in the terminal (no UI)
Put your key in `.env`:
```
ANTHROPIC_API_KEY=sk-ant-...
```
Then:
```bash
.venv/bin/python -m korkut
» kira tespitinde yakın tarihli bir Yargıtay kararı bul
```
Korkut searches Yargıtay live, verifies the decision, and answers in Turkish with citations.
This validates the whole chain (router → redaction → Claude → tools) before the web UI.

## 2. Run the web app (the chat UI)
Set the rest of `.env`:
```
KORKUT_SECRET=<run: python -c "import secrets; print(secrets.token_hex(32))">
# KORKUT_DB_URL unset → a SQLite file under data/ is used for the trial.
```
Create at least one user (there is **no self-service signup** — accounts are admin-only):
```bash
.venv/bin/python -m korkut.api.adduser avukat --name "Av. Ad Soyad"
# prompts for a password (min 8 chars)
```
Start the server:
```bash
.venv/bin/python -m korkut.api        # → http://localhost:8080
```
Open **http://localhost:8080** and log in. **First login forces 2FA setup**: scan the QR with
an authenticator app (Google Authenticator / Authy / 1Password), enter the 6-digit code, and
save the recovery codes shown once. After that, login is password + 6-digit code. On a phone,
use the machine's LAN address (e.g. `http://192.168.1.10:8080`) — responsive web app, no install.

Admin commands: `python -m korkut.api.adduser <user> --reset-2fa` (re-enrol), `--unlock` (clear a
lockout), `--disable` (deactivate + revoke sessions).

## 3. Run with Docker (web app + Postgres)
```bash
# .env must contain ANTHROPIC_API_KEY and KORKUT_SECRET
docker compose -f deploy/docker-compose.yml up -d --build   # → http://localhost:8080
# create a user inside the container:
docker compose -f deploy/docker-compose.yml exec korkut python -m korkut.api.adduser avukat --name "Av. Ad Soyad"
```
`docker compose -f deploy/docker-compose.yml logs -f korkut` for logs; `… down` to stop (`-v` also wipes the DB).

## Notes
- `.env` is gitignored — your keys never get committed.
- Accounts: create more with `python -m korkut.api.adduser <user>`, disable with `--disable <user>`.
- `KORKUT_SECRET` must stay stable across restarts, or everyone is logged out on restart.
- Cost: every Claude call bills your Anthropic account. Opus 4.8 is the default; set
  `ANTHROPIC_MODEL=claude-haiku-4-5` in `.env` for a cheaper trial.
- Redaction (`core/redactor.py`) runs on every cloud call; the DB, hearing/petition modules,
  and production security hardening (HTTPS, 2FA — SECURITY.md) come next.

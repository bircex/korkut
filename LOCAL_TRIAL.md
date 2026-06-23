# Local Trial — run Korkut on your machine

The brain runs on **Claude** (Anthropic) for the trial; switch to OpenAI later by editing
`config/model-routing.yaml` (`backends.cloud: gpt`). Two things only you can do: provide an
**Anthropic API key** and create a **Mattermost bot token**.

## 0. One-time dev setup
```bash
python3 -m venv .venv
.venv/bin/pip install -e ".[dev]" "anthropic>=0.40" "mattermostdriver>=7.3"
cp .env.example .env          # then edit .env (see below)
```

## 1. Quickest check — talk to the brain in the terminal (no Mattermost)
Put your key in `.env`:
```
ANTHROPIC_API_KEY=sk-ant-...
```
Then:
```bash
.venv/bin/python -m korkut
» kira tespitinde yakın tarihli bir Yargıtay kararı bul
```
Korkut will search Yargıtay live, verify the decision, and answer in Turkish with citations.
This validates the whole chain (router → redaction → Claude → tools) before Mattermost.

## 2. Mattermost (already running via Docker)
`docker compose up -d` is done — Mattermost is at **http://localhost:8065**.
(`docker compose ps` to check; `docker compose logs -f mattermost` for logs.)

1. Open http://localhost:8065 → create the **admin account** → create a **team**.
2. Create the bot:
   - Top-left menu → **System Console** → **Integrations → Bot Accounts**.
   - (If disabled: **Integrations → Bot Accounts → Enable Bot Account Creation = true**, Save.)
   - **Add Bot Account**: username **`korkut`**, role member → **Create**.
   - **Copy the access token** it shows once.
3. Back in the team, **add @korkut to a channel** (or just DM it).
4. Put the token in `.env`:
   ```
   MATTERMOST_TOKEN=<the bot token>
   MATTERMOST_BOT_NAME=korkut
   ```

## 3. Run the bridge
```bash
.venv/bin/python -m bridge
```
Now in Mattermost: **DM @korkut**, or type **`@korkut ...`** in a channel. It replies as Korkut,
in a thread, using the legal tools. (Only @mentions / DMs trigger it — channel chatter is ignored.)

## Notes
- `.env` is gitignored — your keys never get committed.
- Cost: every Claude call bills your Anthropic account. Opus 4.8 is the default; set
  `ANTHROPIC_MODEL=claude-haiku-4-5` in `.env` for a cheaper trial.
- Stop everything: `docker compose down` (add `-v` to also wipe the Mattermost/DB volumes).
- This is a trial wiring. Redaction (`core/redactor.py`) runs on every cloud call; the DB,
  hearing/petition modules, and the production security hardening (SECURITY.md) come next.

# Deployment

This covers running Korkut in production. For a local trial, see [INSTALL.md](INSTALL.md).

Korkut is designed to run **on the office's own infrastructure** so client data never leaves it.

## Recommended topology

```
Internet ──► reverse proxy (TLS termination) ──► korkut :8080 (127.0.0.1)
                                                    ├─ dispatcher (scheduled sends)
                                                    └─ integrations (inbound poll)
                                                  Postgres (private network)
```

Put Korkut behind a TLS-terminating reverse proxy (nginx, Caddy, or Traefik). The app binds to
`127.0.0.1:8080` and trusts `X-Forwarded-For` and `Secure` cookies in production.

## Docker (recommended)

`deploy/docker-compose.prod.yml` runs the published image from GHCR against Postgres:

```bash
# 1. Prepare .env (see the variables below); at minimum set a model key,
#    KORKUT_SECRET, and POSTGRES_PASSWORD.
cp .env.example .env

# 2. Start the stack
docker compose -f deploy/docker-compose.prod.yml --env-file .env up -d

# 3. Create the first user
docker compose -f deploy/docker-compose.prod.yml exec korkut \
  python -m korkut.api.adduser avukat@ornek.av.tr --username avukat --name "Av. Name"
```

The image is published by the release pipeline as
`ghcr.io/recepkizilarslan/korkut:vYYYY.MM.PATCH` (and `:latest`). Pin a version in production by
setting `KORKUT_IMAGE` in `.env`.

### Reverse proxy

Terminate TLS at the proxy and forward to `127.0.0.1:8080`, preserving the client IP, e.g. nginx:

```nginx
location / {
    proxy_pass http://127.0.0.1:8080;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

## Environment variables

| Variable | Purpose | Production value |
|---|---|---|
| `KORKUT_SECRET` | Session-cookie signing key | a stable 64-hex string (`python -c "import secrets; print(secrets.token_hex(32))"`) |
| `KORKUT_DB_URL` | Database URL | `postgresql+psycopg://korkut:<pw>@db:5432/korkut` |
| `POSTGRES_PASSWORD` | Postgres password (compose) | a strong secret |
| `KORKUT_HOST` / `KORKUT_PORT` | Bind address/port | `0.0.0.0` / `8080` |
| `KORKUT_SECURE_COOKIES` | `Secure` flag on cookies (HTTPS only) | `1` |
| `KORKUT_TRUST_PROXY` | Read client IP from `X-Forwarded-For` | `1` |
| `KORKUT_DEFAULT_OFFICE` | Name for the first office | optional |
| `GROQ_API_KEY` / `ANTHROPIC_API_KEY` | Cloud model key | one, or use a local endpoint |

The active cloud provider is chosen in `config/model-routing.yaml`. An office can instead point at a
local OpenAI-compatible endpoint from **Settings → Model** (then no PII leaves the machine).

## Security

- Sign-in is Google OIDC only (no passwords); the session is a signed JWT and logout revokes every session server-side.
- PII is redacted before any cloud model call and restored in the reply (`core/redactor.py`). A
  local model endpoint skips redaction because text never leaves the machine.
- Never commit `.env`, `data/`, or backups (enforced by `.gitignore`).
- Report vulnerabilities privately — see [SECURITY.md](../SECURITY.md).

## Backups

- **Database** — back up Postgres regularly (`pg_dump`), encrypt the dumps, and store them off-box.
- **WhatsApp session** (if used) — the Baileys auth state lives in the `data/` volume.

## Workers

The dispatcher (scheduled sends) and integrations poller run as their own services in the prod
compose. On a non-Docker host, schedule them with cron — see `deploy/cron/korkut.crontab`.

# Provisioning runbook (single VPS)

Step-by-step to stand up the Korkut SaaS infra. Ordered so you can do **§1–§5 now** (gets the
auto-deploy pipeline + app live) and **§6–§7 later** (needed for Phase 2 auth / Phase 5 docs).
See [SAAS_ARCHITECTURE.md](SAAS_ARCHITECTURE.md) for why.

> **KVKK / data residency:** the DB holds Turkish clients' legal data. Pick a **TR or EU** region
> for the VPS + backups (e.g. Hetzner EU, or a TR provider). Avoid US-region storage.

## 1. The VPS (do now)

Ubuntu 22.04+, start with ~2–4 vCPU / 4–8 GB (200 offices will want more later).

```bash
# as root on the fresh VPS
adduser deploy && usermod -aG sudo deploy
# Docker + compose plugin
curl -fsSL https://get.docker.com | sh
usermod -aG docker deploy
# firewall: only SSH + HTTP(S)
ufw allow 22 && ufw allow 80 && ufw allow 443 && ufw enable
apt-get install -y fail2ban
mkdir -p /opt/korkut/deploy && chown -R deploy:deploy /opt/korkut
```

**Deploy SSH key** (used by GitHub Actions, separate from your personal key):
```bash
# on your laptop
ssh-keygen -t ed25519 -f korkut_deploy -C "gh-actions-deploy" -N ""
# add the PUBLIC key to the VPS deploy user:
ssh-copy-id -i korkut_deploy.pub deploy@<VPS_IP>
# keep korkut_deploy (private) for the GitHub secret in §4
```

## 2. `.env` on the VPS (do now)

Create `/opt/korkut/.env` (never committed). Generate secrets:
```bash
openssl rand -hex 32     # KORKUT_SECRET
openssl rand -base64 24  # POSTGRES_PASSWORD
openssl rand -base64 24  # REDIS_PASSWORD
```

```dotenv
# /opt/korkut/.env  (chmod 600)
KORKUT_SECRET=<64-hex>
POSTGRES_PASSWORD=<strong>
REDIS_PASSWORD=<strong>
# model provider(s) — operator-controlled (Phase 3+)
ANTHROPIC_API_KEY=
GROQ_API_KEY=
TAVILY_API_KEY=
# Google OIDC (§6, Phase 2)
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
# Telegram single bot (integrations, later)
TELEGRAM_BOT_TOKEN=
# Cloudflare R2 (§7, Phase 5)
R2_ACCOUNT_ID=
R2_ACCESS_KEY_ID=
R2_SECRET_ACCESS_KEY=
R2_BUCKET=korkut-docs
```

## 3. Reverse proxy + TLS (do now)

Put **Caddy** in front (auto HTTPS, terminates TLS, forwards to the app on 127.0.0.1:8080). The prod
compose already sets `KORKUT_SECURE_COOKIES=1` + `KORKUT_TRUST_PROXY=1`.

`/etc/caddy/Caddyfile`:
```
app.korkutai.com {
    encode zstd gzip
    reverse_proxy 127.0.0.1:8080
    header {
        Strict-Transport-Security "max-age=31536000; includeSubDomains; preload"
        X-Content-Type-Options nosniff
        X-Frame-Options DENY
    }
}
```
Install Caddy as a system service (`apt install caddy`), or run it as another compose service.

## 4. GitHub Actions secrets (do now)

Repo → Settings → Secrets and variables → Actions → **New repository secret**:

| Secret | Value |
|---|---|
| `VPS_HOST` | VPS IP / hostname |
| `VPS_USER` | `deploy` |
| `VPS_SSH_KEY` | contents of the **private** `korkut_deploy` key |
| `VPS_DEPLOY_PATH` | `/opt/korkut` |
| `GHCR_PAT` | a GitHub **fine-grained PAT**, scope **read:packages** only (VPS uses it to pull the private image) |

Then trigger a deploy: push to `main` (CI → Deploy) or run the **Deploy** workflow manually
(`workflow_dispatch`). First run pulls Postgres/Redis + the app image and starts the stack.

## 5. Cloudflare DNS (do now)

Domain is already on Cloudflare. Add an **A record**: `app` → `<VPS_IP>`.
- **Recommended:** Proxied (orange) for WAF/CDN, and set SSL/TLS mode to **Full (strict)** using a
  **Cloudflare Origin Certificate** installed in Caddy (`tls origin.pem origin.key` block).
- **Simplest to start:** DNS-only (grey cloud) → Caddy gets a Let's Encrypt cert directly. You lose
  Cloudflare's proxy benefits; switch to proxied+Full(strict) before launch.

Check: `https://app.korkutai.com` serves the app.

## 6. Google OAuth (before Phase 2 — auth)

Google Cloud Console → new project → **OAuth consent screen** (External; add app name, support email,
TR) → **Credentials → Create OAuth client ID → Web application**:
- Authorized redirect URI: `https://app.korkutai.com/api/auth/google/callback`
- Copy Client ID + Client secret → `.env` (`GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`).

## 7. Cloudflare R2 (before Phase 5 — documents)

Cloudflare → R2 → **Create bucket** `korkut-docs` (keep **private**, public access blocked). Create
an **R2 API token** scoped to Object Read & Write for that bucket → note Account ID, Access Key ID,
Secret → `.env`. Objects are stored per-office (`office/{id}/...`) with SSE + short-lived signed URLs.

## 8. Postgres app role (handled in code — Phase 1)

You do **not** hand-craft this. The Phase-1 DB bootstrap creates a dedicated **non-owner,
`NOBYPASSRLS`** app role + RLS policies, and the deploy smoke-test asserts the app isn't a superuser.
The compose Postgres is fine as-is for now.

## Order of operations
1. Now: §1 VPS → §2 .env → §3 Caddy → §4 secrets → §5 DNS → first deploy of the current image.
2. Phase 2: §6 Google OAuth. Phase 5: §7 R2. Backups (encrypted, off-box) + monitoring before launch.

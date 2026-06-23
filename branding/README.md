# Branding (white-label)

Brand: **Cankar Hukuk** — black/white, Themis (justice) logo.

## What's done (free, Team Edition)
- **Name everywhere:** `SITE_NAME` (docker-compose / `.env`) → tab title, header, emails show
  "Cankar Hukuk" instead of "Mattermost". Verified live.

## Dark (black/white) theme
Forcing an org-wide theme is an **Enterprise** feature (ignored on free Team Edition). Free ways:
1. **Per user (simplest):** each user → Settings → Display → Theme → **"Mattermost Dark"**.
   For a small office this is a one-time click per account.
2. **True forced black/white + logo:** inject custom CSS + logo (see below).
3. **Enterprise:** the built-in Custom Branding UI sets logo + theme in one place (paid).

## Logo
Put a **clean, owned** logo here as `branding/logo.png` (and `logo.svg` if you have it).

> ⚠️ The image first shared carried a **"MINH KHUE" watermark** — that is another firm's
> stock asset. Do not ship it. Commission/obtain a logo Cankar Hukuk owns. Also note generic
> "Lady Justice / Themis" clip-art often has its own license terms — verify before use.

### Free white-label via reverse proxy (recommended for production)
Put nginx/Caddy in front of Mattermost (we need it anyway for TLS, SECURITY.md §1) and have it
inject a small `<style>` (brand colors, hide/replace the Mattermost logo) and serve
`branding/logo.png` at the logo path. This gives a real black/white branded login + header on
the free edition, no per-user step. To be wired when we set up the reverse proxy in Phase 0.

`logo.png` and other binaries here are gitignored (see `.gitignore`); keep the source/owned
asset in the office's own asset store, not the repo.

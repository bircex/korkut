# Security Design

> Goal: **single gateway Mattermost** into the system, access from mobile, **2FA mandatory**, no
> access from anywhere else. Client data stays in the office (see PLAN.md principles, ARCHITECTURE.md §5).

## 1. Principle: Single gateway = Mattermost
- The **only service exposed to the internet is Mattermost** (HTTPS / 443). Nothing else is
  reachable from outside.
- `asistan` (Python), Postgres, `tools/`, OpenAI calls → all **localhost / internal network**,
  behind the firewall. The user cannot reach any of these directly.
- A **reverse proxy** in front (Caddy/nginx) publishes only Mattermost; **automatic TLS**
  (Let's Encrypt). The mobile app requires a valid certificate.

```
Internet ──TLS/443──> reverse proxy ──> Mattermost ──localhost──> asistan ─┬─> Postgres
                                                                            ├─> tools/
                                                                            └─> OpenAI (redacted, only external call)
  (every other port: bound to localhost only, CLOSED to the outside)
```

## 2. Network hardening
- **Firewall (ufw):** only 443 open. Everything else closed.
- **Port binding:** Postgres (5432), Mattermost internal (8065), `asistan` → bound only to
  `127.0.0.1`; not exposed externally.
- **SSH (administration):** password disabled, **key only**; and open only to the admin IP / VPN
  (fully closed if possible, manage via console/VPN). [KARAR-G2]
- **fail2ban** + brute-force protection.

## 3. Identity & MFA (front and center in the UI)
- **MFA/2FA mandatory (TOTP):** Mattermost System Console → *Enforce Multi-factor
  Authentication*. On first login the user is **forced** through setup (Google Authenticator/
  Authy). No login without MFA.
- **Open registration DISABLED:** *Enable Open Server = false*. Accounts are created only **by
  invitation/admin**. Email verification mandatory.
- **Strong password policy:** min. length + uppercase/lowercase/symbol; login attempt limit.
- **Session:** reasonable duration + idle timeout. On mobile, an in-app **PIN/biometric lock**.

## 4. Mobile access
- Mattermost mobile app; server URL = our domain name (TLS required).
- **Push notification privacy [KARAR-G1]:** MM mobile push by default goes through Mattermost's
  hosted push proxy (HPNS) → notification **content** may go to a third party. Unacceptable
  for a law office. Options:
  - **(a) id-only push (recommended):** no content in the notification ("you have a new message");
    content is fetched from the server when the app opens → no data leaks, easy to set up.
  - **(b) Self-host your own push proxy:** full control, more setup.
  - At minimum **(a)** will be applied.
- (Optional) Installation on approved devices only via **MDM**.

## 5. Data security
- **Disk:** LUKS full disk encryption (PLAN.md Phase 0). DB **backups also encrypted**.
- **Everything sent to the cloud is redacted** (redaction/pseudonymization, ARCHITECTURE.md §5). If id-only push is chosen,
  the **only external call is OpenAI**, and that too is redacted.
- Secret keys (.env) do not go into the repo; protected on the server with file permissions.

## 6. Monitoring
- Mattermost access/audit logs; failed login alerts.
- (Compliance export is an Enterprise feature — not needed for now.)

## 7. Permission matrix
| Direction | Permission |
|-----|------|
| Internet → Mattermost 443 (TLS) | ✅ single gateway |
| Internet → everything else | ❌ closed |
| Mobile → Mattermost (MFA + TLS) | ✅ |
| asistan ↔ Mattermost / Postgres / tools | ✅ localhost only |
| asistan → OpenAI (redacted) | ✅ only external call |
| Admin SSH | key + VPN/IP (or closed) |

## 8. Decisions
- **KARAR-G1 — Push privacy:** id-only (a) or self-host proxy (b)? (Default: a)
- **KARAR-G2 — SSH:** Restricted SSH for administration (key + IP/VPN), or fully closed +
  VPN-only?

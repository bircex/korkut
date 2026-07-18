# Python runtime environment contract (KDM-002)

Frozen inventory of every environment variable the current Python backend reads,
with its **name, semantics, default, and where it is read** — captured for .NET
parity. **No secret values are recorded**; only names/semantics/defaults that are
already public in code (via `os.getenv(..., default)`).

Source of truth: `grep` of `os.getenv` / `os.environ` across
`backend/src/korkut` (2026-07-18). Re-verify with:

```bash
grep -rnE 'os\.(getenv|environ)' backend/src/korkut
```

## Loading

`korkut/core/env.py::load_dotenv()` is a dependency-free `.env` loader: it reads
`KEY=VALUE` lines from `ENV_FILE` (`korkut/paths.py`) and sets them into
`os.environ` **without overriding** already-set vars. The real `.env` holds the
secret values and is never committed (`.env.example` documents the shape).

## Core / app

| Name | Semantics | Default | Read in |
|---|---|---|---|
| `KORKUT_ROOT` | Overrides the resolved project root used for paths. | derived | `paths.py:21` |
| `KORKUT_SECRET` | Signing secret for session cookies / auth tokens. **Secret.** | none (required) | `api/auth.py:32` |
| `KORKUT_DB_URL` | SQLAlchemy DB URL (app connection). | `_DEFAULT_URL` (sqlite) | `api/store.py:44` |
| `KORKUT_HOST` | API bind host. | `0.0.0.0` | `api/__main__.py:18` |
| `KORKUT_PORT` | API bind port. | `8080` | `api/__main__.py:19` |
| `KORKUT_SECURE_COOKIES` | `1` ⇒ mark session cookie `Secure`. | `0` | `api/app.py:391` |
| `KORKUT_TRUST_PROXY` | `1` ⇒ trust `X-Forwarded-For` for client IP. | `0` | `api/app.py:395` |
| `KORKUT_ALLOW_DEMO` | `1` ⇒ enable demo-request flow. | `1` | `api/app.py:719` |

## Database migrator / provisioning (least-privilege split)

| Name | Semantics | Default | Read in |
|---|---|---|---|
| `KORKUT_ADMIN_DB_URL` | Elevated DB URL used only to bootstrap/provision. **Secret.** | none | `api/bootstrap.py:38` |
| `KORKUT_APP_DB_PASSWORD` | Password granted to the least-privileged app role. **Secret.** | none | `api/bootstrap.py:39` |
| `KORKUT_APP_DB_ROLE` | Name of the app DB role (non-owner, non-BYPASSRLS). | `korkut_app` | `api/bootstrap.py:43` |

> Confirms the intended app-role / migrator-role separation exists in the Python
> baseline already; the .NET migrator (KDM-011) must preserve these names.

## SMTP / notifications

| Name | Semantics | Default | Read in |
|---|---|---|---|
| `KORKUT_SMTP_FROM` | From address (falls back to `KORKUT_SMTP_USER`). | — | `api/store.py:354` |
| `KORKUT_SMTP_USER` | SMTP username. **Secret-ish.** | — | `api/store.py:354` |
| `KORKUT_SMTP_PASSWORD` | SMTP password. **Secret.** | — | `api/store.py:355` |
| `KORKUT_SMTP_FROM_NAME` | Display name for outgoing mail. | `Korkut` | `api/store.py:360` |
| `KORKUT_SMTP_HOST` | SMTP host. | `smtp.gmail.com` | `api/store.py:362` |
| `KORKUT_SMTP_PORT` | SMTP port. | `587` | `api/store.py:363` |
| `KORKUT_ADMIN_NOTIFY_EMAIL` | Comma-list of admin notification recipients. | `""` | `api/store.py:386` |

## Google OIDC

| Name | Semantics | Default | Read in |
|---|---|---|---|
| `GOOGLE_CLIENT_ID` | OAuth client id. | none | `api/google_oidc.py:48` |
| `GOOGLE_CLIENT_SECRET` | OAuth client secret. **Secret.** | none | `api/google_oidc.py:49` |
| `GOOGLE_REDIRECT_URI` | OAuth redirect URI. | none | `api/google_oidc.py:50` |
| `KORKUT_ADMIN_REDIRECT_URI` | Admin-panel OAuth redirect URI (present in `.env`). | none | `.env` (admin flow) |

Google auth is treated as *configured* only when id+secret+redirect are all set
(`google_oidc.config()`), surfaced publicly as `google_configured` in `/api/config`.

## LLM providers & models

| Name | Semantics | Default | Read in |
|---|---|---|---|
| `ANTHROPIC_API_KEY` | Anthropic key — read implicitly by the SDK. **Secret.** | none | `core/providers/claude.py:31` (`anthropic.Anthropic()`) |
| `ANTHROPIC_MODEL` | Anthropic model id. | `claude-opus-4-8` | `core/providers/claude.py:18` |
| `ANTHROPIC_MAX_TOKENS` | Max output tokens for Anthropic. | `16000` | `core/providers/claude.py:19` |
| `OPENAI_API_KEY` | OpenAI key — read implicitly by the SDK. **Secret.** | none | `core/providers/gpt.py:42` (`OpenAI()`) |
| `OPENAI_MODEL` | OpenAI model id. | `gpt-4o` | `core/providers/gpt.py:30` |
| `GROQ_API_KEY` | Groq key (OpenAI-compatible). **Secret.** | none | `core/providers/groq.py:30` |
| `GROQ_MODEL` | Groq model id. | `llama-3.3-70b-versatile` | `core/providers/groq.py:19` |

## Context budget

| Name | Semantics | Default | Read in |
|---|---|---|---|
| `KORKUT_CONTEXT_WINDOW` | Cloud model context window (tokens). | `128000` | `brain/context_budget.py:30` |
| `KORKUT_LOCAL_CONTEXT_WINDOW` | Local model context window (tokens). | `8192` | `brain/context_budget.py:29` |
| `KORKUT_REPLY_HEADROOM` | Tokens reserved for the reply. | `1024` | `brain/context_budget.py:33` |

## Tool search keys

Tools declare `requires_key: <ENV_NAME>` in their `tool.yaml`; the registry
(`core/tool_registry.py:40`) **hides** a tool whose key env var is unset (graceful
degradation). The base web-search provider uses `TAVILY_API_KEY`
(`tools/_base/search_providers.py:33`). **Secret.** Per-tool key names come from
each `tool.yaml` and must be enumerated when migrating the toolset (KDM-033).

## ⚠️ Compatibility findings (record, do not change here)

- **`KORKUT_MODELS_FILE` is NOT used by the Python runtime.** No code reads it
  (`grep` returns nothing across `.py/.yaml/.md/.env`). The **actual** model
  configuration is two sources: (a) per-provider **default** model ids from the
  `ANTHROPIC_MODEL` / `OPENAI_MODEL` / `GROQ_MODEL` env vars (defaults above),
  and (b) the **per-office selected model** from the DB catalog
  (`RegisteredModel`/`OfficeModel`, `store.office_model_cfg`), with routing rules
  in `config/model-routing.yaml`. The decision docs mandate a `KORKUT_MODELS_FILE`
  JSON contract; that is a **future/aspirational** contract, not part of the
  current baseline. The .NET port (KDM-033) must preserve the **actual** env +
  DB + YAML contract and treat `KORKUT_MODELS_FILE` as new work, not a regression.
- **`KORKUT_SUPERADMIN_EMAILS`** is not read anywhere (matches the rule to keep it
  a dead documentation variable; admin allowlist is compiled-in per KDM-020).
- API keys for Anthropic/OpenAI are consumed by the vendor SDKs from the
  environment; they must remain operator-managed env/secret and never be moved
  into DB/UI/admin (enforced by KDM-020/KDM-033 rules).

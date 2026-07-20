# Korkut .NET container images (KDM-009)

Hardened, minimal, reproducible images for the three .NET hosts. Build context is
the repository root.

| Image | Dockerfile | Base (runtime) | Shape |
|---|---|---|---|
| `korkut-api` | `Dockerfile.api` | `aspnet:10.0-noble-chiseled` | HTTP API, port 8080 |
| `korkut-worker` | `Dockerfile.worker` | `runtime:10.0-noble-chiseled` | long-running outbox processor |
| `korkut-migrator` | `Dockerfile.migrator` | `runtime:10.0-noble-chiseled` | one-shot; applies migrations then exits |

## Hardening properties

- **Multi-stage** — the SDK stage builds/publishes; the final image ships only the
  published app on a chiseled runtime (no shell, no package manager, no SDK,
  no source, no tests).
- **Non-root** — chiseled images run as the `app` user (UID 1654). Never root.
- **Digest-pinned bases** — SDK `sdk:10.0.102` (matches `global.json`) and the
  chiseled runtimes are pinned by multi-arch index digest for reproducibility.
- **Read-only filesystem** — the apps write nothing to the root filesystem; run
  with `--read-only --tmpfs /tmp`. Verified in the smoke test.
- **No baked config/secrets** — runtime configuration comes only from the
  environment; `.dockerignore` keeps `.env*` (except `.env.example`), `bin/`,
  `obj/`, `tests/` and docs out of the context.
- **Graceful shutdown** — the .NET Generic Host stops on SIGTERM; the API drains
  and the worker honours its stopping token (KDM-008). Verified in the smoke test.

## Build & run

```bash
cd dotnet
docker build -f docker/Dockerfile.api      -t korkut-api      .
docker build -f docker/Dockerfile.worker   -t korkut-worker   .
docker build -f docker/Dockerfile.migrator -t korkut-migrator .

# API, hardened runtime flags:
docker run --read-only --tmpfs /tmp --cap-drop ALL --security-opt no-new-privileges \
  -e ASPNETCORE_URLS=http://+:8080 -p 8080:8080 korkut-api

# Migrator (one-shot):
docker run --rm --read-only --tmpfs /tmp korkut-migrator info
```

## Health

Chiseled images ship no shell/curl, so there is no in-image Docker `HEALTHCHECK`;
liveness/readiness are the `/health/live` and `/health/ready` endpoints (KDM-007),
probed by the orchestrator. The smoke test verifies them from the host.

## Smoke test

`docker/smoke-test.sh` builds all three images and asserts non-root, one-shot
migrator exit, health on a read-only filesystem, and graceful SIGTERM shutdown.
It runs in CI via `.github/workflows/container-smoke.yml` when the packaging
changes.

## Updating base image digests

```bash
docker buildx imagetools inspect mcr.microsoft.com/dotnet/sdk:10.0.102 --format '{{json .Manifest}}'
```

Use the index `digest` (multi-arch), not a platform-specific manifest digest, so
the image builds on both amd64 (CI) and arm64 (dev).

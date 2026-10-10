# Development

The day-to-day developer loop. To install and use Korkut, see [INSTALL.md](INSTALL.md); for the
system design, see [ARCHITECTURE.md](ARCHITECTURE.md).

## Layout

```
src/
  services/       hosts: Korkut.Api (the app), Korkut.Ocr, Korkut.KikProxy, Korkut.Worker,
                  Korkut.Migrator, and Korkut.Web (shared web plumbing)
  modules/        feature modules (Clients, Matters, Documents, Knowledge, Agent.*, Tools, ...)
  shared/         kernel, contracts, persistence, security, AI runtime
tests/            unit, integration (Testcontainers Postgres) and architecture tests
ui/               the React app (Vite), built into the app image
website/          the Astro marketing site, a separate static deploy
templates/        petition/contract skeletons + intake schemas, shipped in the app image
docker/           Dockerfiles and the image smoke test
```

`make help` lists the shortcuts below.

## Running it

The simplest loop is the full stack, rebuilt after a change:

```bash
make up                      # docker compose up -d --build, http://localhost:8090
```

For UI work with hot reload, keep the stack running and start Vite, pointing it at the stack:

```bash
VITE_DEV_API_TARGET=http://localhost:8090 make ui-dev    # http://localhost:5173
```

## Tests and checks

```bash
make test        # .NET: unit + integration + architecture (needs Docker for Postgres)
make ui-test     # React: type-check + vitest
cd website && npm run check && npm run build
```

CI runs the same on every pull request (`.github/workflows/`), plus the container smoke test
(`docker/smoke-test.sh`) when images or the UI change.

The OpenAPI document is snapshotted. After an intentional API change, regenerate it and review the
diff:

```bash
KORKUT_OPENAPI_UPDATE=1 dotnet test tests/Korkut.Api.Tests --filter OpenApiSnapshot
```

## Database migrations

Each context keeps its migrations next to it (operational: `src/shared/Korkut.Infrastructure`,
agent: `src/shared/Korkut.Agent.Persistence`, knowledge: `src/modules/Korkut.Knowledge`). The
migrator applies all of them when the stack starts. Row-level security and the privileged resolver
functions are hand-written SQL inside the migrations; new tenant tables need an RLS policy there.

## Adding an assistant tool

Tools live in `src/modules/Korkut.Tools`. A tool is a class implementing `IAgentTool`; the app
registers every implementation in that assembly. Keep the offered surface tight: prefer one tool
with a `source` parameter over many near-identical ones.

# Korkut .NET solution (KDM-005)

.NET 10 modular-monolith scaffold for the Korkut backend migration. Lives
side-by-side with the current Python runtime (`../backend/`); neither shares a
database migration owner (per the architecture decision). This scaffold builds
clean under warnings-as-errors + nullable and has architecture, smoke and unit
tests. Domain logic is added per module in later KDM issues.

## Layout

```
dotnet/
  Korkut.slnx                  solution (folders: src/, tests/)
  Directory.Build.props        net10.0, nullable, warnings-as-errors, analyzers
  Directory.Packages.props     central package management (one version list)
  .editorconfig                analyzer policy (intentional rule tuning documented)
  src/
    Korkut.SharedKernel        UUIDv7, IClock, Result/Error — primitives only, no deps
    Korkut.Domain              entities/aggregates; depends on SharedKernel only
    Korkut.Application         command/query/validation/authorization (Domain only)
    Korkut.Infrastructure      EF Core/Npgsql/MinIO/providers (KDM-011+); impl of Application
    Korkut.Api                 ASP.NET Core host; health endpoints; composition root
    Korkut.Worker              outbox/OCR/indexing host skeleton (KDM-008+)
    Korkut.Migrator            separate least-privileged schema-migration host (KDM-011)
  tests/
    Korkut.Domain.Tests        SharedKernel/domain unit tests
    Korkut.Architecture.Tests  NetArchTest layer-boundary rules
    Korkut.Api.Tests           WebApplicationFactory health smoke
```

## Dependency rules (enforced by Korkut.Architecture.Tests)

```
SharedKernel  ← Domain ← Application ← Infrastructure ← Api / Worker
                                    ↖ Migrator
```

- Domain depends only on SharedKernel; never on Application/Infrastructure/host
  or on EF Core/Npgsql/ASP.NET/MinIO.
- Application depends on Domain (+ SharedKernel); never on Infrastructure/host or
  infra frameworks.
- SharedKernel depends on nothing else in the solution.

These are asserted at test time and fail the build if violated (verified with a
negative-control during development).

## Build & test

```bash
cd dotnet
dotnet restore Korkut.slnx
dotnet build   Korkut.slnx -c Release   # warnings-as-errors, analyzers, nullable
dotnet test    Korkut.slnx -c Release   # architecture + smoke + unit (15 tests)
```

CI: `../.github/workflows/dotnet-migration-ci.yml` runs restore/build/test on
every PR into `dotnet-migration` that touches `dotnet/**`.

## Notes

- **Migrations never run on API/Worker startup.** Only `Korkut.Migrator` applies
  schema changes, under a separate least-privileged identity (KDM-011).
- Env-var names bind to typed Options with their **exact existing names**
  (KDM-033); see `docs/migration/baseline/ENV_CONTRACT.md`.
- No brain/RAG/vector-DB code here — out of migration scope until after KDM-037.

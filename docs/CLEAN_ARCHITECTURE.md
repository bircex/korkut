# Korkut Backend — Clean Architecture (target design)

Status: **rollout complete** — every domain below was migrated domain-by-domain (each its own green commit), and the conventions here now describe the code, not just the target.

What landed:

- Every operational domain (§4) is a full vertical slice; the old grouped `*Endpoints.cs` / `Features/` / `*Entities.cs` are gone. `Api/Endpoints` and `Infrastructure/Persistence/Entities` no longer exist — every persisted type, operational **and** Agent, is a Domain aggregate EF-mapped directly (no `record` + `*Entity` + `ToDomain` split anywhere).
- Every host serves through controllers (Api, Knowledge, Ocr); no minimal-API endpoint groups remain.
- Auth (local sign-in / lockout / recovery, Google OIDC, onboarding, refresh tokens, 2FA), Offices + the tenant root, Members/Invites/OfficeRoles, Auditing, Jobs, Knowledge (relocated; the pgvector chunk row stays in Infrastructure by design — Domain must not depend on pgvector), and the Agent-plane entities + use-case app-services are all on these rules.

Still genuinely future work, not migration: a full **Agent host** rebuild (the Agent code is aligned to these rules but currently unwired — no host serves chat/drafting/sessions yet) and the **Gateway** (no project exists yet). Both are feature/design work against these same conventions.

---

## 1. Principles

1. **Classic Clean Architecture.** Four rings, dependencies point inward only:

   ```
   Domain  ←  Application  ←  Infrastructure
                    ↑                ↑
                   Web  ────────────/
                    ↑
                   Api (host / composition root)
   ```

   - **Domain** depends on nothing.
   - **Application** depends on Domain only.
   - **Infrastructure** depends on Application + Domain (implements its ports).
   - **Web** = shared host cross-cutting (error handler, tenancy middleware, JSON, auth) — depends on Application.
   - **Api** = the host / composition root. Wires Application interfaces → Infrastructure implementations. Controllers live here.

2. **One type per file.** No more `OfficeCoreModels.cs` with 3 records, no `IOfficeCoreRepositories.cs` with 3 interfaces. Every entity, interface, service, DTO, request and response is its own file named after the type.

3. **Vertical slices, one folder per domain.** Every domain is a direct top-level folder in every layer — `Domain/Clients/`, `Application/Clients/`, `Infrastructure/Clients/`, `Api/Clients/`. No umbrella/grouping level above a domain. Sub-split only inside a domain when it grows (Requests/, Responses/, Dtos/).

4. **One entity, owned by the Domain.** No `record` in Domain + `*Entity` in Infrastructure + hand-written `ToDomain`. There is **one** entity class, in Domain, and EF maps it directly via a Fluent `IEntityTypeConfiguration<T>` in Infrastructure. Column/table names are preserved, so **no migration changes**.

5. **Everything derives from a base.** Entities derive from `Entity` and implement the marker interfaces. Repositories derive from `IRepository<T>` / `Repository<T>`. Tenancy is mandatory in the base repo — every query is office-scoped by construction.

6. **Controllers are thin.** A controller action forwards to a service and returns the result. No business logic, no DbContext, no mapping in controllers.

---

## 2. Base primitives

### Domain/Common (the shared kernel, now inside Domain)

```csharp
// IEntity.cs
public interface IEntity { Guid Id { get; } }

// ITenantOwned.cs — the single most important rule: every row carries its office.
public interface ITenantOwned { Guid OfficeId { get; } }

// ISoftDeletable.cs
public interface ISoftDeletable { DateTimeOffset? DeletedAt { get; } }

// IAggregateRoot.cs — marker for the root of a consistency boundary (what a repo serves).
public interface IAggregateRoot;

// Entity.cs — id-keyed base, equality by id.
public abstract class Entity : IEntity, IEquatable<Entity>
{
    protected Entity(Guid id) => Id = id;
    public Guid Id { get; private set; }
    protected static Guid NewId() => UuidV7.NewId();
    public bool Equals(Entity? o) => o is not null && o.GetType() == GetType() && o.Id == Id;
    public override bool Equals(object? o) => o is Entity e && Equals(e);
    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
```

> `ITenantOwned` / `ISoftDeletable` move **out of Infrastructure into Domain** — they are domain contracts, and the base repo needs them.

### Application/Common (the base ports)

```csharp
// IRepository.cs — tenant-scoped generic persistence. officeId is always first.
public interface IRepository<TEntity> where TEntity : Entity, ITenantOwned, IAggregateRoot
{
    Task<TEntity?> FindAsync(Guid officeId, Guid id, CancellationToken ct = default);        // no-tracking read
    Task<TEntity?> FindForUpdateAsync(Guid officeId, Guid id, CancellationToken ct = default); // tracked, for mutation
    Task<IReadOnlyList<TEntity>> ListAsync(Guid officeId, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid officeId, Guid id, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// ISoftDeleteRepository.cs — for aggregates retired by a timestamp; its reads exclude them.
public interface ISoftDeleteRepository<TEntity> : IRepository<TEntity>
    where TEntity : Entity, ITenantOwned, IAggregateRoot, ISoftDeletable
{
    Task<bool> SoftDeleteAsync(Guid officeId, Guid id, DateTimeOffset deletedAt, CancellationToken ct = default);
}

// ICurrentUser.cs — the caller's office/user/permissions, from the trusted token only.

// IUnitOfWork.cs — opens the per-request tenant scope (sets app.current_office for RLS) and commits.
public interface IUnitOfWork
{
    Task<T> InTenantScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default);
}
```

Per-domain repositories **extend** a base and add only their specific queries (often none):

```csharp
// Application/Clients/IClientRepository.cs
public interface IClientRepository : ISoftDeleteRepository<Client>;
```

### Infrastructure/Common (the base implementation — the only place a DbContext is touched)

```csharp
// Repository.cs — abstract EF base; the ONLY holder of DbContext outside its subclasses.
public abstract class Repository<TEntity>(KorkutDbContext db) : IRepository<TEntity>
    where TEntity : Entity, ITenantOwned, IAggregateRoot
{
    protected KorkutDbContext Db => db;
    protected DbSet<TEntity> Set => db.Set<TEntity>();
    protected IQueryable<TEntity> Scoped(Guid officeId) => Set.Where(e => e.OfficeId == officeId);
    // FindAsync / ListAsync / AddAsync / SaveChangesAsync implemented here.
}
```

```csharp
// Infrastructure/Clients/ClientRepository.cs
public sealed class ClientRepository(KorkutDbContext db) : SoftDeleteRepository<Client>(db), IClientRepository { /* specific queries */ }

// Infrastructure/Clients/ClientConfiguration.cs — EF maps the DOMAIN entity directly.
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> b)
    {
        b.ToTable("clients");
        b.HasKey(x => x.Id);
        b.Property(x => x.OfficeId).HasColumnName("office_id");
        // ... same column names as today → migrations unchanged.
    }
}
```

---

## 3. The canonical vertical slice

Every domain looks **exactly** the same across the four layers, and every domain is a **direct top-level folder** in its layer — there is no grouping/context level above it. Example: `Clients`.

```
Domain/
  Clients/
    Client.cs                           // Client : Entity, ITenantOwned, ISoftDeletable, IAggregateRoot

Application/
  Clients/
    IClientRepository.cs                // : IRepository<Client>
    IClientService.cs                   // use-case port
    ClientService.cs                    // use-case impl (orchestrates the repo; pure, no I/O)
    Requests/
      CreateClientRequest.cs
      UpdateClientRequest.cs
    Responses/
      ClientResponse.cs

Infrastructure/
  Clients/
    ClientRepository.cs                 // : Repository<Client>, IClientRepository
    ClientConfiguration.cs              // IEntityTypeConfiguration<Client>

Api/
  Clients/
    ClientsController.cs                // thin: forwards to IClientService
```

**Where each kind of type lives:**

| Kind | Layer | Folder |
|---|---|---|
| Entity (single, EF-mapped) | Domain | `Domain/<Domain>/` |
| Repository **interface** | Application | `Application/<Domain>/` |
| Service **interface + impl** (use-case) | Application | `Application/<Domain>/` |
| Request / Response / DTO | Application | `Application/<Domain>/{Requests,Responses,Dtos}/` |
| Repository **impl** | Infrastructure | `Infrastructure/<Domain>/` |
| EF configuration | Infrastructure | `Infrastructure/<Domain>/` |
| Controller | Api | `Api/<Domain>/` |

Sub-split only *inside* a domain when it genuinely grows (`Requests/`, `Responses/`, or a nested aggregate folder) — never by adding a grouping layer above it.

> **Use-case services are Application, not Infrastructure.** They only orchestrate repository ports + domain + `IClock`; no DbContext, no external I/O. A "service" that does direct I/O (object store, external HTTP, e-mail) is an **infrastructure adapter behind an Application port** (e.g. the upload pipeline, storage) — those stay in Infrastructure. Repository *implementations* are always Infrastructure.

**Naming:** `I<Agg>Repository` / `<Agg>Repository`, `I<Agg>Service` / `<Agg>Service`, `<Verb><Agg>Request`, `<Agg>Response`, `<Agg>Controller`. No `Core` / `Implementations` / `Abstractions` grouping folders anywhere.

---

## 4. Full target folder tree (operational scope)

Every domain below is its **own top-level folder** in each of Domain / Application / Infrastructure / Api, and each becomes the identical vertical slice from §3. There is no `OfficeCore`-style umbrella.

```
Clients/          müvekkiller                        ← done (the reference slice)
Matters/          dosyalar
Hearings/         duruşmalar
Courts/           mahkeme referans verisi
Parties/          taraflar / vekiller
Users/            kullanıcılar
Offices/          ofisler (tenant kökü)
Members/          ofis üyeliği + rol atama
Invites/          davetler
Roles/            ofise özel roller
Auth/             refresh token, parola sıfırlama, TOTP/2FA
Tasks/            görevler
Deadlines/        süreler
Notes/            görüşme notları
Invoices/         faturalar
Expenses/         masraflar
Events/           takvim
Correspondence/   evrak
Channels/         kanallar
Settings/         ofis ayarları
Templates/        şablon override'ları (+ salt-okunur katalog)
Outbox/           giden mesajlar
Documents/        belgeler, extraction, relation
Upload/           validator / malware / safe-upload   ← already in this shape
Storage/          object store portu
Reports/          salt-okunur sorgu servisleri (entity/repo yok)
Auditing/         denetim kayıtları
Knowledge/        RAG chunk'ları (Knowledge host kullanır)
Jobs/             Worker için iş kuyruğu altyapısı
```

Cross-cutting, shared:

```
Domain/Common/           IEntity, ITenantOwned, ISoftDeletable, IAggregateRoot, Entity
                         + Ids / Results / Time / Redaction (the former SharedKernel)
Application/Common/      IRepository, IUnitOfWork
Application/Errors/      NotFoundException, BadRequestException, ...
Infrastructure/Common/   Repository (EF base)
Infrastructure/Persistence/  KorkutDbContext, SnakeCaseNaming, Tenancy, Migrations
Web/                     ApiExceptionHandler, tenancy middleware, JSON, auth (unchanged)
```

---

## 5. Deferred (not in this pass)

- **Agent / AI** (conversations, generated documents, chat, drafting, model routing, LLM ledger) and **Gateway** were removed from the solution. They will be rebuilt against these exact rules in a later pass ("AI tarafında da aynı şekilde").
- **OCR** becomes its own service later. It needs **no database** — it depends on Application only (an `ITextExtractor`/OCR port + the extraction use-case). Kept out of the persistence graph.

---

## 6. Migration order

Each step is its own commit; the solution stays green (build + tests) after every one.

1. **Base primitives** — move `ITenantOwned`/`ISoftDeletable` to `Domain/Common`, add `IEntity`/`Entity` usage, promote the generic `IRepository<T>` / `Repository<T>` to `Application/Common` + `Infrastructure/Common` (from the agent-only copy).
2. **Reference slice: `Clients`** — full vertical per §3 (entity + EF config + repo + service + controller + requests/responses). This is the template every other domain copies. **Done.**
3. Roll out the rest domain by domain in the §4 order, starting with `Matters` and `Hearings`.
4. Delete the old grouped files (`*Models.cs`, `*Entities.cs`, `I*Repositories.cs`, `*Repositories.cs`, `Features/`, `Persistence/Configurations/*`) as each domain is migrated — the `OfficeCore` folders disappear with them.
5. Move Api from grouped minimal-API `*Endpoints.cs` to per-aggregate controllers.
```

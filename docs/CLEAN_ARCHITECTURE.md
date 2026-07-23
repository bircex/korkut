# Korkut Backend — Clean Architecture (target design)

Status: **proposed blueprint** — review the conventions here, then we migrate domain-by-domain (each domain a separate green commit). Agent/AI and Gateway are **deferred** (removed for now, redesigned later against these same rules).

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

3. **Vertical slices by domain, then by aggregate.** Every layer is split the same way: `<Context>/<Aggregate>/...`. Sub-split when an aggregate grows (Requests/, Responses/, Dtos/).

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
    Task<TEntity?> FindAsync(Guid officeId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<TEntity>> ListAsync(Guid officeId, CancellationToken ct = default);
    Task AddAsync(TEntity entity, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(Guid officeId, Guid id, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

// IUnitOfWork.cs — opens the per-request tenant scope (sets app.current_office for RLS) and commits.
public interface IUnitOfWork
{
    Task<T> InTenantScopeAsync<T>(Guid officeId, Func<Task<T>> work, CancellationToken ct = default);
}
```

Per-aggregate repositories **extend** the base and add only their specific queries:

```csharp
// Application/OfficeCore/Clients/IClientRepository.cs
public interface IClientRepository : IRepository<Client>
{
    Task<bool> UpdateAsync(Guid officeId, Guid id, string name, string kind, string? email, string? phone, CancellationToken ct = default);
}
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
// Infrastructure/OfficeCore/Clients/ClientRepository.cs
public sealed class ClientRepository(KorkutDbContext db) : Repository<Client>, IClientRepository { /* specific queries */ }

// Infrastructure/OfficeCore/Clients/ClientConfiguration.cs — EF maps the DOMAIN entity directly.
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

Every aggregate looks **exactly** the same across the four layers. Example: `OfficeCore / Clients`.

```
Domain/
  OfficeCore/
    Clients/
      Client.cs                         // Client : Entity, ITenantOwned, ISoftDeletable, IAggregateRoot

Application/
  OfficeCore/
    Clients/
      IClientRepository.cs              // : IRepository<Client>
      IClientService.cs                 // use-case port
      ClientService.cs                  // use-case impl (orchestrates the repo; pure, no I/O)
      Requests/
        CreateClientRequest.cs
        UpdateClientRequest.cs
      Responses/
        ClientResponse.cs

Infrastructure/
  OfficeCore/
    Clients/
      ClientRepository.cs               // : Repository<Client>, IClientRepository
      ClientConfiguration.cs            // IEntityTypeConfiguration<Client>

Api/
  OfficeCore/
    Clients/
      ClientsController.cs              // thin: forwards to IClientService
```

**Where each kind of type lives:**

| Kind | Layer | Folder |
|---|---|---|
| Entity (single, EF-mapped) | Domain | `Domain/<Ctx>/<Agg>/` |
| Repository **interface** | Application | `Application/<Ctx>/<Agg>/` |
| Service **interface + impl** (use-case) | Application | `Application/<Ctx>/<Agg>/` |
| Request / Response / DTO | Application | `Application/<Ctx>/<Agg>/{Requests,Responses,Dtos}/` |
| Repository **impl** | Infrastructure | `Infrastructure/<Ctx>/<Agg>/` |
| EF configuration | Infrastructure | `Infrastructure/<Ctx>/<Agg>/` |
| Controller | Api | `Api/<Ctx>/<Agg>/` |

> **Use-case services are Application, not Infrastructure.** They only orchestrate repository ports + domain + `IClock`; no DbContext, no external I/O. A "service" that does direct I/O (object store, external HTTP, e-mail) is an **infrastructure adapter behind an Application port** (e.g. the upload pipeline, storage) — those stay in Infrastructure. Repository *implementations* are always Infrastructure.

**Naming:** `I<Agg>Repository` / `<Agg>Repository`, `I<Agg>Service` / `<Agg>Service`, `<Verb><Agg>Request`, `<Agg>Response`, `<Agg>Controller`. No `Core` / `Implementations` / `Abstractions` grouping folders anywhere.

---

## 4. Full target folder tree (operational scope)

Contexts and their aggregates. Each cell below becomes the identical vertical slice from §3 across Domain / Application / Infrastructure / Api.

```
Identity/        Users, Offices, RefreshTokens, Invites, TwoFactor, Roles, PasswordReset
OfficeCore/      Clients, Matters, Hearings
OfficeOps/       Tasks, Deadlines, Notes
Finance/         Invoices, Expenses
Calendar/        Events
Correspondence/  Correspondence           (evrak)
Channels/        Channels
Membership/      Members                  (office-scoped membership + role assignment)
Settings/        OfficeSettings
Templates/       TemplateOverrides        (+ the read-only TemplateCatalog)
Outbox/          OutboundMessages
Documents/       Documents, Extractions, Relations, Upload(*done), Storage
Reports/         (read-only query services — no aggregate/repo)
Auditing/        AuditEvents
Knowledge/       Chunks                   (RAG store, used by the Knowledge host)
Jobs/            (infra job stores for the Worker)
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
2. **Reference slice: `OfficeCore/Clients`** — full vertical per §3 (entity + EF config + repo + service + controller + requests/responses). This is the template every other slice copies.
3. Roll out the remaining OfficeCore aggregates (Matters, Hearings), then context by context in the §4 order.
4. Delete the old grouped files (`*Models.cs`, `*Entities.cs`, `I*Repositories.cs`, `*Repositories.cs`, `Features/`) as each context is migrated.
5. Move Api from grouped minimal-API `*Endpoints.cs` to per-aggregate controllers.
```

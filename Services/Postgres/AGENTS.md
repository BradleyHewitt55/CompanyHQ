# Postgres service

`Services/Postgres` is the only place CompanyHQ talks to PostgreSQL. Other services define models;
they never open connections, never define a `DbContext`, and never reference Npgsql directly.

It does not own any domain rule, and it does not know what a `Post` or a `Message` is — it discovers
`BaseModel` subclasses by assembly scan and serves generic CRUD, paging, migrations, seeding, and a
health check.

`AccessPermissions` is the stable permission vocabulary and `McpActionAttribute` is the controller
metadata contract shared with MCP without adding a project dependency from a domain service to MCP.

## Shared foundation and permission vocabulary

Feature services may reference Postgres for persistence and shared permission names. They must not
open their own database connections or reference Npgsql directly.

Permission names live in `Contracts/AccessPermissions.cs`. Add every new name to `All`: Auth
registers its authorization policy from that list, and User's seeder grants it to the built-in Admin
role on the next boot. Custom roles receive it only when an operator explicitly grants it. When
`Postgres:AutoSeed` is disabled, ship a migration that adds the permission to the Admin row.

## Defining a model

Inherit `BaseModel` — that is the whole contract. Inheriting is what makes the type discoverable,
mappable, and usable with `IPostgresService<T>`.

`BaseModel` gives `Id`, `CreatedAt`, `UpdatedAt`. **Never set them by hand** — IDs are generated when
an entity is created, and `PostgresDbContext` stamps timestamps on save. An explicitly reset empty ID
is filled by the context as a fallback:

- `Id` — UUIDv7, generated at construction. Time-ordered, so inserts append to the primary-key
  index instead of scattering across it; `Guid.Empty` is replaced during save if a caller resets it.
- `CreatedAt` — written once, explicitly not modifiable on update.
- `UpdatedAt` — refreshed on every update.

Indexes, constraints, and column types go in an `IEntityTypeConfiguration<T>` in the same assembly as
the model; the registry applies configurations from every assembly it scanned.

Tables and columns are snake_case (`Post` → `post`, `CreatedAt` → `created_at`) so raw SQL never needs
quoted `"PascalCase"` identifiers.

## Using it

Use `IPostgresService<T>` by default. Inject `PostgresDbContext` when writes must commit across
multiple tables; separate repository calls are separate transactions.

- `Query()` returns an **untracked** `IQueryable<TModel>` for filtering, paging, and projections.
- `AddAsync` / `AddRangeAsync` / `UpdateAsync` / `DeleteAsync` **save immediately**. When several
  models must commit together, inject `PostgresDbContext` and call `SaveChangesAsync` once — created
  IDs are already available for tracking and timestamps are stamped there.
- `DeleteAsync` returns `false` when no row matched, which maps cleanly to a 404.

## Paging

Take a `PageRequest` from the query string and call `PageAsync`, or `ToPagedResultAsync` when the
filter needs `Query()`. Never hand-roll count/skip/take in a controller.

- **In a controller, never name the `[FromQuery] PageRequest` parameter `page`.** MVC then binds the
  whole complex type from the `?page=` value and silently hands back defaults. Call it `paging`.
- **Always let it order.** Ordering defaults to `CreatedAt` descending, tie-broken by `Id`. Postgres
  may return an unordered query in any order, so `Skip`/`Take` can repeat a row on one page and drop
  it from the next. Pass `orderBy` only for a different total order.
- The parameter order is `(page, selector, orderBy, cancellationToken)` — pass the token **by name**,
  positionally it lands in `orderBy` and will not compile.
- `PageRequest` clamps itself: `Page` floors at 1, `PageSize` clamps to 1…`MaxPageSize` (100),
  defaulting to `DefaultPageSize` (10). A nonsense page number is not worth a 400.
- `PagedResult<T>` computes `TotalPages` / `HasPrevious` / `HasNext` so clients do no paging
  arithmetic. Use `PagedResult<T>.Empty(request)` for the no-results shortcut.

## Searching

Every list endpoint takes the same two query parameters, bound as `[FromQuery] SearchRequest`:
`?field=title&keyword=launch`. The service passes it to `query.Search(...)` with **the DTO type the
caller is about to receive**, and that type is the whole allow-list.

- **The searchable fields are the DTO's own.** A field is searchable when the shape has a property
  of that name and the model has a scalar column behind it. Nothing is listed by hand, so a new DTO
  field is searchable the day it is added, and a column no shape carries (a password hash, a token)
  can never be searched.
- **That is what makes it role specific.** A reader's shape has no `IsDraft`, so `?field=isDraft`
  is a 400 for them and a filter for an editor. Pass the audience's shape, never the admin one
  because a route happens to be restricted today.
- With no `field`, the keyword matches any **text** field of the shape. With one, any scalar
  works: a number, date, boolean, id or enum is compared as Postgres prints it.
- The match is a case-insensitive contains, in SQL. `%`, `_` and `\` in the keyword are literals.
- A field the shape spells differently from the model, or reaches through a navigation, is an
  alias: `["RoleName"] = "AccessRole.Name"`. Keep the map beside the projection it describes.
- An unknown field throws `SearchFieldException`. The host's `SearchFieldExceptionFilter` turns it
  into a 400 that lists the fields the caller may use; a controller does not catch it.
- A list that is not a Postgres query (GitHub issues) honours `keyword` and ignores `field`.

## Response audiences

Entity responses use the role shapes defined by the shared service template. A wider shape
**derives from the narrowest one** only when it adds fields that audience may see. One endpoint
serves the shapes; an authorized operator gets the same JSON with extra properties.

```csharp
[JsonDerivedType(typeof(AdminPostDto))]          // no discriminator: extra fields, no $type
public record ClientPostDto(Guid Id, string Title, string Slug, string Content, DateTimeOffset CreatedAt);

public sealed record AdminPostDto(/* … */, DateTimeOffset UpdatedAt)
    : ClientPostDto(Id, Title, Slug, Content, CreatedAt);
```

The rules that make this safe:

- **Name them `{Role}{Class}Dto`** — `ClientPostDto` and `AdminPostDto`, never a bare `PostDto`. The
  audience is part of the type's identity, so a variable's declared type says which shape it holds.
  Role variants share a file named after the class, as required by the shared service template.
- **The admin DTO is always a superset.** It derives, so it cannot drop a field the client shape
  promises. A consumer deserialising into the client record ignores the extras — which is why the
  mobile app needed no change for the entities it reads.
- **`ResponseAudience.Client` is `0`**, so `default(ResponseAudience)` is the *narrow* shape. Every
  service method defaults its `audience` parameter to it. A forgotten argument under-shares; it can
  never over-share.
- **The audience is a parameter, not ambient state.** Never read `IHttpContextAccessor` — `Apps/Web`
  is not the only caller: an MCP adapter runs outside a controller and passes the audience itself.
- **Controllers resolve it with `User.Audience(permission)`**
  (`Services/User/Extensions/ResponseAudienceExtensions.cs`): the operator shape only for a caller
  whose role grants that permission, `Owner` for anyone else signed in, `Client` for an anonymous
  one, so a missing token widens nothing. Never hard-code `ResponseAudience.Admin` because a route
  happens to be restricted today.
- **Projections do the switching, in SQL.** `…Projections.For(audience)` returns an `Expression`
  typed to the *client* DTO whose body constructs the admin one behind an upcast. EF Core reads
  through the cast to the constructor, so the wider shape still costs one query and materialises no
  entity.

A password hash is never in either shape.

## Registration

The API registers `AddPostgres` first, then `AddUsers`, then the other services. A persisted model
needs no individual registration: discovery follows the host's project dependency graph for both
runtime and `dotnet ef`.

`AddPostgres(configuration, hostAssembly)` registers the shared `NpgsqlDataSource` (with
NetTopologySuite for PostGIS), the model registry, `PostgresDbContext`, the open generic
`IPostgresService<>`, the one hosted service that migrates then seeds, and the `postgres` health
check at `GET /health`. The host passes its own assembly: it holds the migrations and is where model
discovery starts.

**Models are discovered, never listed.** `PostgresModelRegistry.Discover` reads the host's
`deps.json` and scans every project or package library that is this assembly or references it — the only
assemblies that can declare a `BaseModel`. A service that gains its first model is mapped by being
referenced from the host; nothing here or in `Apps/Api` changes. The design-time factory runs the same
discovery from the same assembly, so a generated migration can never drop a table the host expects.
The corollary: a project the host does not reference is not mapped.

A company host consumes these services as packages and passes its own assembly, so it owns one
migration set for the whole schema: the generic tables and its own. HQ's migrations in `Apps/Api`
belong to HQ's host only and are not what a company applies.

## Migrations

Migrations live in the API assembly (`Apps/Api/Migrations`) and are applied by
`PostgresMigrationService` during `StartAsync`, before the host accepts traffic — a migration
failure stops startup rather than serving requests against a half-built schema. `Scripts/start-dev.ps1
-Api` creates a timestamped migration only if `dotnet ef migrations has-pending-model-changes`
reports a model difference. Review and commit the generated migration; production only applies
migrations already built into the API. Set `Postgres:AutoMigrate` to `false` wherever two hosts could
start at once and race.

```bash
cd Apps/Api && dotnet ef migrations add AddPetTable
```

`dotnet ef` uses `Apps/Api/Data/PostgresDesignTimeFactory.cs`, which calls
`CreateDesignTimeContext` — the same binding, validation, and model discovery as `AddPostgres` — so
the generated migration matches what runs.

## Seeding

Implement `IDataSeeder` and register it scoped. `PostgresSeedService` runs every registration in
`Order` after migrations, **on every boot** — so a seeder must be **idempotent**: check for the row
before writing it.

`Services/User` uses this for the admin account — see `Services/User/AGENTS.md`.

Every seeder gets the **same** `PostgresDbContext` instance, resolved once for the whole loop. A
seeder that leaves dirty tracked entities behind pollutes the next one, which then saves changes it
never made. Save what you add before returning.

## Configuration

Section `Postgres` in `appsettings.json`:

| Key | Default | Notes |
| --- | --- | --- |
| `ConnectionString` | — | Wins over the composed fields below. |
| `Host` / `Port` / `Database` / `Username` / `Password` | `localhost` / `5432` / `companyhq` / `companyhq` / — | From the environment as `Postgres__Host` … `Postgres__Password`. |
| `CommandTimeoutSeconds` | `30` | Stamped onto the connection string even when `ConnectionString` supplied one. |
| `MaxRetryCount` / `MaxRetryDelaySeconds` | `5` / `10` | EF Core transient-failure retries. |
| `MinPoolSize` / `MaxPoolSize` | `1` / `50` | Always win over the connection string. |
| `AutoMigrate` | `true` | Migrate on startup. |
| `EnableSensitiveDataLogging` | `false` | Logs parameter values. Development only. |

Missing or invalid configuration throws during `AddPostgres`, not on the first query — an empty
`Password` included, because it reaches the server as a trust-auth attempt and fails with an error
that names nothing useful.

`docker-compose.yml` fills the image's `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` from
the same `Postgres__Database` / `Postgres__Username` / `Postgres__Password` keys the API binds, so the
container and the application cannot end up on different credentials. Compose reads `.env` itself;
the API loads it via `Apps/Api/Data/EnvironmentFile.cs`, which walks up from the binary to the
repository root and never clobbers variables already set.

Changing `Postgres__Password` after the volume exists does nothing — the image only applies it on
first initialisation. Either `ALTER USER companyhq WITH PASSWORD '…'` on the running container, or
drop the volume and let it re-initialise.

## Rules

- No `DbContext` outside this project.
- No persisted model without `BaseModel`.
- No mock data — exercise against the real `postgis/postgis:17-3.5` container.
- A person's own location is never persisted. Server-owned geodata uses PostGIS
  `geography(Point, 4326)`; NetTopologySuite is already wired up for it.

### Two traps that have each bitten several services

**A user-initiated transaction must run inside the execution strategy.** The host configures
`NpgsqlRetryingExecutionStrategy`, which refuses a bare `BeginTransactionAsync` with
*"does not support user-initiated transactions"*. Open the transaction **inside**
`context.Database.CreateExecutionStrategy().ExecuteAsync(...)` so the whole unit is retriable, and
call `context.ChangeTracker.Clear()` at the top of the delegate so a retry cannot reuse entities
tracked by the failed attempt. The delegate runs more than once — nothing captured outside it may
assume a single execution. Moderation and Message both shipped this bug; it throws at
runtime, not at compile time, so only an integration test catches it.

**Filter, order and count over entities — project last.** Building an `IQueryable<SomeDto>` and then
applying `Where`, `OrderBy` or `Count` to it makes EF try to translate a predicate whose subject is a
`new SomeDto(...)` expression, and it throws. Keep the entity as the query root through filtering,
ordering, counting and paging, and project only the page. Moderation's `GET /api/blocks`, the post
list and the conversation list each shipped this and each returned 500 in production.

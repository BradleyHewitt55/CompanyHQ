# Provider boundaries and coordinated upgrade

## Release policy

These changes must ship in a **new major release**, not a replacement of any existing tag/package.
No release/tag/version is selected by this implementation. All service and Contracts packages plus
CompanyHQ.Api are discovered automatically by `.github/workflows/publish-packages.yml`, packed at
one immutable tag version and consumed at a single pinned version. Persistence, Cache.Contracts,
Redis, OpenStreetMap, Rss and Rss.Contracts are included by that discovery. The UniversalApi experiment
is outside the package tree, not shipped or automatically enabled in production.

Upgrade every CompanyHQ package together and rebuild the host and any source consumer. Do not mix old
feature binaries with new neutral constructor dependencies. This is storage/wire compatible, **not
universally source/binary compatible**. Existing companies can remain on their old immutable release.

## Inventory and capability decisions (#5)

| Existing need | Neutral path | Provider/deployment exception |
| --- | --- | --- |
| Appointment, Blog, Company, FAQ, GitHub, Media object CRUD | IRepository<T>, snapshots, projections, ordered pages, search | Owning `Data` EF declarations only |
| User/Auth roles, token issue/rotation/revocation, account deletion | IPersistence staged tracked multi-model writes; retry-safe transactions; explicit row locks | None in normal paths |
| MCP single-use exchanges and refresh rotation | Atomic filtered update + tracked transaction | None in normal paths |
| Inbox replies/activity, Message participants/history/idempotency | Tracked unit, joins/correlated subqueries, query functions, transactional decisions | None in normal paths |
| Email/Notification durable workers | Explicit Locked(skipLocked), bounded claim/lease commit before provider delivery | FOR UPDATE implementation is solely Postgres |
| Moderation target-wide histories/hides/restrictions | Serializable multi-model transaction, shape-limited search and paging | None in normal paths |
| Model discovery, migrations, seed execution | Provider implements host composition/discovery; neutral seed contract | DbContext, migrations, Npgsql pool remain Postgres/host |
| Index/relationship/column/PostGIS declarations | Models stay in features; inherited shared base unchanged in storage | EF Relational package is deliberately retained **only for owning-assembly `IEntityTypeConfiguration<T>` declarations**, including Auth/Data relationships; feature Services must not call EF/Npgsql |
| Genuine geography/raw SQL | Not made into a portable query | Use a separately named host/provider adapter capability if needed; no feature raw SQL remains in current normal paths |

Persistence is a closed relational query interface, not IQueryable in disguise with an escape hatch.
It exposes bounded operators, expression descriptions, materialization, projection, joins, correlated
subqueries, shape-safe search, explicit row locking, atomic bulk update/delete, tracked staging and
transactions. It contains no reference to EF, Npgsql or Postgres implementation. Provider expressions
are rejected and unsupported expressions must fail, not fall back to in-memory scans. This is **not**
a claim that every non-relational backend could implement these relational semantics.

The original `CompanyHQ.Services.Postgres.Contracts.Models.BaseModel` now actually lives in the
neutral Persistence assembly and is type-forwarded from CompanyHQ.Services.Postgres. The neutral
name derives from it; both old company models and migrated feature models remain assignable to the
historic base, and old/new repository generic constraints accept it. UUIDv7 and timestamp behavior
is provider-owned as before. Model class names/assemblies and all mapped tables/columns remain
unchanged. Direct reflection on the immediate base/assembly can observe the new neutral layer.
Shared paging, permission and audience wire shapes retain Postgres.Contracts; this assembly has
no database dependencies despite its historic name.

The old IPostgresService<T>, PostgresDbContext, queryable extensions and old seed registration remain
explicit compatibility/provider APIs. Normal feature constructors changed to IRepository/IPersistence,
and AdminSeeder now uses the neutral seed contract: callers directly constructing concrete services
must migrate/rebuild. Neutral writes expose UniqueConstraintException, PersistenceConcurrencyException
or PersistenceException; transport exhaustion exposes PersistenceUnavailableException, not a data
race. Old provider repository callers keep their EF-facing semantics. SearchFieldException's historic
subtype remains catchable while also deriving from the neutral exception used by new callers.

## Host changes (#3/#4/#6)

```csharp
services.AddPostgres(configuration, hostAssembly); // supplies Persistence too
services.AddCache(configuration);
services.AddRedis(configuration);                 // new explicit adapter
services.AddGeo(configuration);
services.AddOpenStreetMap(configuration);         // new explicit adapter
services.AddRss(configuration);                   // injectable only, no HTTP route
```

Apps/Api and the company starter already use this composition. Custom hosts must add both adapters;
missing required ports fail startup validation, not silently install alternate providers.

### Cache compatibility

ICacheService, CacheOptions properties/config keys, JSON/null payloads, existing namespaces, TTLs,
cache-aside and exception policy are unchanged. Redis owns connection parsing/multiplexer/SCAN/Lua.
Do not flush Redis. `CacheOptions.BuildConfiguration()` **cannot remain an instance method returning
StackExchange.Redis.ConfigurationOptions while Cache has no Redis SDK reference**; it was removed.
Code using that provider-specific method must use
`CompanyHQ.Services.Redis.Contracts.Models.RedisOptions.BuildConfiguration()` with the same
ConnectionString (or preferably AddRedis). Direct CacheService/CacheHealthCheck construction now takes
ICacheProvider rather than the SDK multiplexer. This is a deliberate major-version source/binary break.

Web continues using its own Node Redis client and `companyhq:web:refresh:*` coordination on the same
instance. Nothing in these edits changes its key format, credentials or refresh flow. Do not sweep
`web:` keys as feature invalidation. No unnecessary Web migration is needed.

### Geo compatibility

Routes, DTOs, errors/auth/rate limits, rounded coordinate keys, tile v3 payload/grid/soft-hard TTL,
empty-vs-failure and partial-outage behavior are unchanged. GeoOptions keeps its former transport
properties as obsolete source shims; Geo no longer reads them. OpenStreetMap binds the **same Geo
configuration section**, including endpoints, User-Agent and pacing. Do not rename configuration.

IGeoService consumers keep their surface. The public GeoService constructor now accepts IGeoProvider,
not NominatimClient; NearbyTileStore similarly takes the provider. Former public Nominatim/Overpass
clients, response types and limiters moved to CompanyHQ.Services.OpenStreetMap namespaces/assembly;
forwarding them from Geo would introduce the forbidden Geo -> implementation dependency. Direct
transport consumers must change namespace/package and rebuild, or use IGeoProvider/IGeoService.
The HTTP and client-shippable Geo.Contracts surface is unchanged; do not call this a transparent
binary move. Old GeoOptions members are compatibility only, not a second configuration owner.

### RSS integration

Server features can reference Rss.Contracts/IRssService and hosts add Rss. Call FetchAsync with URL
and opaque validators or DiscoverAsync with a website URL. Changed can legitimately contain no items;
NotModified reuses previous items; Failed retains previous state/validators. No automatic Article
policy, database, schedule or public URL endpoint is introduced. PawGuard adoption remains deferred.
Returned HTML is untrusted; consumers sanitize at rendering. Every XML/media element honors xml:base.
The transport pins validated DNS answers at connect time and validates every redirect, with bounded
size/time/decompression and prohibited DTD/XXE. Tests use no live publisher site.

## Schema and consumer checks

Do not rewrite migrations or snapshots for this split. Both the EF model-differ regression test and
`dotnet ef migrations has-pending-model-changes --project Apps/Api/Api.csproj --no-build` verify no schema
delta; the disposable PostGIS fixture applies existing migrations before exercising new ports.
The search regression also caught Npgsql's implicit empty LIKE escape: the adapter now explicitly
uses backslash so %, _ and backslash retain their documented literal semantics.

Run `dotnet test Tests/Services.IntegrationTests.csproj`, build Apps/Api, compare generated OpenAPI
(`node Scripts/check-openapi.mjs` with validation-only startup configuration), and build an actual
created company against **locally packed** packages at one temporary version before release. No
publishing credentials/deployed databases are required for these checks. Experimental two-language
acceptance/measurements are `python Experiments/UniversalApi/verify.py`.

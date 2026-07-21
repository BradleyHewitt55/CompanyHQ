# Postgress

This guide is for hosts and plugins that need persistence. Postgress is the PostgreSQL
provider plugin ([`PostgressPlugin`][1], EF Core + Npgsql). Consumers never reference
it directly — you code against the contracts in `Abstractions.Data` and load this
plugin to make PostgreSQL the store. See [Data access](../../../Docs/Architecture/DataAccess.md)
for the decision behind that split.

## I want to store and read data from my plugin

Inherit [`BaseModel`][2] and inject the generic services. No registration, no mapping
code — inheriting `BaseModel` is the table registration.

```csharp
public sealed class Invoice : BaseModel
{
    [Required, MaxLength(64)]
    public string Number { get; set; } = "";
    public Guid UserId { get; set; } // cross-plugin refs are id-only
}
```

```csharp
public sealed class InvoiceService(
    IRepository<Invoice> repository,
    IQueryService<Invoice> queries)
{
    public Task<Invoice> Create(Invoice invoice, CancellationToken ct)
        => repository.AddAsync(invoice, ct);

    public Task<PagedResult<Invoice>> Recent(int page, CancellationToken ct)
        => queries.PageAsync(page, pageSize: 20,
            orderBy: i => i.Number, descending: true, cancellationToken: ct);
}
```

Writes go through [`IRepository<T>`][3] ([`PostgressRepositories<T>`][4]); reads
through [`IQueryService<T>`][5] ([`PostgressQueries<T>`][6]). Reads are no-tracking by
contract — don't mutate an entity from the query service and expect it to save; fetch
it through the repository instead.

## I want to host the plugin

Each company gets its own database (see [Companies](../../../Docs/Architecture/Companies.md));
which one a call hits is decided by the scoped `ICompanyContext`. The platform
database — shared state that belongs to no company — is optional and configured as:

```json
{
  "ConnectionStrings": {
    "CompanyHq": "Host=localhost;Database=companyhq;Username=companyhq;Password=..."
  }
}
```

Resolution fails loudly if no company is selected and no platform string exists —
that is intentional; there is no silent in-memory fallback.

## I want a company's database hosted on this server

Pick "Host on this server" in the `companyhq --c <CompanyName>` flow (or the
fallback dashboard gate; see [Companies](../../../Docs/Architecture/Companies.md)). [`CompanyDatabases`][10] provisions a
dedicated database + role on the local Postgres server, generates the password,
writes `Companies/{Name}/db.json`, and syncs the schema immediately — no
credentials are ever typed. Provisioning needs an admin login on the server:

```json
{
  "ConnectionStrings": {
    "CompanyHqServer": "Host=localhost;Database=postgres;Username=postgres;Password=..."
  }
}
```

`db.json` holds the generated connection string in plain text — the company
directory is the ownership and backup boundary, so protect it like a secret store.

## I want to use an external database for a company

Pick "Use an external database" in the CLI flow or fallback dashboard gate and paste the
connection string the company already pays for. It is stored verbatim in
`Companies/{Name}/db.json` (`managed: false`); the schema sync runs against it
the same as a locally hosted one. No `CompanyHqServer` admin login is needed for
external databases. (Scripted setups can pass the string to
`ICompanyRegistry.RegisterAsync` instead.)

## I want the schema to follow my plugins

Nothing to do — the plugin registers [`SchemaMigrator`][9], a hosted service that
runs before the host serves traffic. On a fresh database it creates the full schema
from the model; on an existing one it scaffolds the live schema, diffs it against
the runtime model, and applies **additive** changes (new tables, columns, indexes)
automatically. There are no `dotnet ef migrations` commands to run, ever.

Destructive operations (drops, type changes) are logged as warnings and skipped.
To let a restart apply them:

```json
{
  "Postgress": { "AllowDestructiveMigrations": true }
}
```

Leave that off outside development — a restart should never silently drop data.
For a destructive change in production, apply the SQL manually and restart.

## I want mapping the conventions can't express

Attributes first (`[Index]`, `[MaxLength]`, `[Required]` — see
[Data access](../../../Docs/Architecture/DataAccess.md#constraints-attributes-first)). If no
attribute can express the rule (composite keys, seed data), register an
[`IModelContributor`][7] in your plugin's `ConfigureServices`; the provider's
[`PostgressDbContext`][8] applies every contributor after convention mapping.

```csharp
services.AddSingleton<IModelContributor, InvoiceModelContributor>();
```

## Verify it worked

Resolve any `IRepository<T>` and round-trip an entity, or check the table exists:

```bash
psql -d companyhq -c "\dt"
```

Every concrete `BaseModel` subclass in a loaded assembly should appear as a table.

## Common mistakes

- **Referencing the Postgress project from a consumer.** Only provider projects may
  reference a database driver; consumers use `Abstractions.Data` only.
- **Editing entities fetched via `IQueryService<T>`.** Queries are no-tracking;
  `UpdateAsync` on a repository-fetched entity is the write path.
- **Paging without `orderBy`.** It works — [`PostgressQueries<T>`][6] falls back to
  ordering by `Id` — but Guid v7 order is creation order, so pass an explicit
  `orderBy` when the user expects anything else.
- **Expecting a new plugin's tables without a restart.** No hot reload: model
  discovery and schema sync run at startup only.
- **Expecting a column drop or type change to just happen.** Only additive changes
  apply automatically; check the startup log for skipped-operation warnings.
- **Downgrading the project SDK.** Postgress looks headless but is a Razor Class
  Library like every plugin — see [Plugins](../../../Docs/Architecture/Plugins.md).

[1]: PostgressPlugin.cs
[2]: ../../Abstractions/Data/BaseModel.cs
[3]: ../../Abstractions/Data/IRepository.cs
[4]: Services/PostgressRepositories.cs
[5]: ../../Abstractions/Data/IQueryService.cs
[6]: Services/PostgressQueries.cs
[7]: ../../Abstractions/Data/IModelContributor.cs
[8]: PostgressDbContext.cs
[9]: Services/SchemaMigrator.cs
[10]: Services/CompanyDatabases.cs

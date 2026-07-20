# Data Access

## Decision

Persistence is a headless, pluggable service. Consumers only ever use the generic
interfaces in `Abstractions`; each database is its own plugin project that implements
them. Loading a database plugin *is* the provider choice — swapping databases means
loading a different plugin, never changing consumer code.

- **Contracts** live in `Projects/Abstractions/Data`:
  - [`BaseModel`][1] — abstract base every persisted model inherits (Guid v7 `Id`).
    Inheriting it IS the table registration: providers discover every concrete
    `BaseModel` subclass in loaded assemblies and map it by convention. All db
    services, current and future, constrain on `where T : BaseModel`.
  - [`IRepository<T>`][2] — generic CRUD (get by id, add, update, delete).
  - [`IQueryService<T>`][3] — read side: filter, list, page, count via expressions.
  - [`IModelContributor`][4] — last-resort escape hatch for mapping that neither
    conventions nor attributes can express (composite keys, seed data).
- **Providers** live under `Projects/Services/` as one plugin per database.
  Current: [`Postgress`][5] (EF Core + Npgsql) — see the
  [Postgress service README](../../Projects/Services/Postgress/README.md). Only provider projects may
  reference a database driver.

## Configuration

```json
{
  "ConnectionStrings": { "CompanyHq": "Host=localhost;Database=companyhq;Username=...;Password=..." }
}
```

## Consuming

Inject [`IRepository<T>`][2] for writes and [`IQueryService<T>`][3] for reads. To
persist a new model from a plugin, inherit [`BaseModel`][1] — that's it, the table is
discovered automatically. Add an [`IModelContributor`][4] only when the default
mapping isn't enough. No DbContext access outside provider projects.

## Constraints: attributes first

Declare constraints on the model with data annotations, not contributors:
`[Index(nameof(Email), IsUnique = true)]` (class-level; one attribute per unique
rule, multiple columns in one attribute = the combination is unique), `[MaxLength]`,
`[Required]`. `Id` is always the primary key. Reach for [`IModelContributor`][4] only
when no attribute can express the rule.

## Relationships

A relationship comes from one model referencing another — inheriting [`BaseModel`][1]
alone never links two tables.

- Across plugin boundaries: store the foreign id only (`Guid UserId`). Keeps
  plugins decoupled; no project references another plugin's models.
- Within a plugin: navigation properties are fine. One-to-many creates the FK
  automatically (no join table); many-to-many (collections on both sides) creates
  the join table automatically.
- Keep persisted models self-contained: scalars + references to other `BaseModel`
  types. Any other referenced class gets mapped as a table too.

## Plugin changes and the schema

No hot reload by design. Adding/enabling a plugin requires an app restart; on
startup, discovery in the provider's [`DbContext`][6] picks up the new `BaseModel`
subclasses and [`SchemaMigrator`][7] syncs every database — the platform one and
each registered company's (see [Companies](Companies.md)) — to the model. Cheap
restarts over runtime model-cache and migration machinery.

There are no design-time migrations (`dotnet ef migrations`) on purpose: the model
is composed at runtime from whichever plugins a deployment loads, so no checked-in
snapshot can describe it. Instead the migrator scaffolds the live database, diffs
it against the runtime model, and applies **additive changes only** (new tables,
columns, indexes). Destructive operations are logged and skipped unless
`Postgress:AllowDestructiveMigrations` is set — a restart must never silently drop
data. See the [Postgress service README](../../Projects/Services/Postgress/README.md) for configuration.

[1]: ../../Projects/Abstractions/Data/BaseModel.cs
[2]: ../../Projects/Abstractions/Data/IRepository.cs
[3]: ../../Projects/Abstractions/Data/IQueryService.cs
[4]: ../../Projects/Abstractions/Data/IModelContributor.cs
[5]: ../../Projects/Services/Postgress/PostgressPlugin.cs
[6]: ../../Projects/Services/Postgress/DbContext.cs
[7]: ../../Projects/Services/Postgress/Services/SchemaMigrator.cs

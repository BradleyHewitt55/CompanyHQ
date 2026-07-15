# Root CompanyHQ Postgres database

## Goal

Run one Postgres instance for a customer-owned CompanyHQ server, with its persistent data and operational configuration rooted at `{server-root}/CompanyHQ/`. The server owner manages database lifecycle from that root directory; company applications receive only their own database credentials.

## Target layout

```text
{server-root}/
  CompanyHQ/
    docker-compose.yml
    .env                         # server-level Postgres secret; never committed
    data/
      postgres/                  # persistent Postgres data directory
    database/
      init/                      # idempotent server bootstrap SQL/scripts
      backups/                   # encrypted database backups; excluded from Git
    Companies/
      acme/
        company.json
        .env                     # Acme app connection string/secret only
        Admin/
        Projects/
      beta/
        company.json
        .env                     # Beta app connection string/secret only
```

## Database model

One Postgres container runs at the server root. It owns a separate database and least-privilege role for every company:

```text
Postgres container
  company_acme  <- company_acme_app role
  company_beta  <- company_beta_app role
```

The server-level SQLite registry may store only installation metadata such as company ID, hostname, database name, and status. It must not store company profiles, users, services, orders, or other tenant data.

## Developer-facing Database service

Developers must not write raw SQL, receive a database connection, or depend directly on Postgres or SQLite libraries. Replace the developer-facing Postgres and SQLite persistence surfaces with one provider-neutral `Database` service.

```text
Developer service
  -> IDatabaseService<T> where T : BaseModel
  -> Database service
  -> selected provider adapter (Postgres or SQLite)
  -> company database
```

`BaseModel` moves to and is owned by the `Database` service as the required provider-neutral persistence contract. Only concrete models that inherit `BaseModel` are discovered for persistence; each discovered model creates and owns its own standalone table in that company's selected database. A model that does not inherit `BaseModel` must never be added to a database table.

The Database service exposes the same safe generic operations for every provider: create, get by ID, update, delete, list, pagination, sorting, and typed/parameterized field filters. It must not expose `DbConnection`, `DbCommand`, `IQueryable`, `FromSql`, provider contexts, or any raw-SQL execution method.

Postgres and SQLite remain internal provider adapters. Provider selection remains configuration-driven, but user service code calls the same `IDatabaseService<T>` contract regardless of whether the company uses Postgres or SQLite.

## Tasks

- [ ] Move the Postgres Compose service and its persistent volume/bind mount to `{server-root}/CompanyHQ/docker-compose.yml` and `{server-root}/CompanyHQ/data/postgres/`.
- [ ] Add a server bootstrap command that creates a company database, an application role, a generated secret, and only the privileges required for that database.
- [ ] Store the generated connection string only in `{server-root}/CompanyHQ/Companies/{company-slug}/.env` or a secrets manager; do not place per-company passwords in Compose or source control.
- [ ] Change company creation to register the company database name and hostname in the SQLite registry without writing tenant data there.
- [ ] Update application configuration so Acme connects only to `company_acme` and Beta only to `company_beta`.
- [ ] Add backup and restore commands operating from `{server-root}/CompanyHQ/database/`; test a restore into an isolated temporary database.
- [ ] Add a migration for existing per-company Postgres data directories into the root Postgres instance without merging tenant databases.
- [ ] Create a provider-neutral `Database` service with `IDatabaseService<T>` constrained to `T : BaseModel`. Postgres and SQLite become internal adapters behind this contract, with identical supported operations and semantics.
- [ ] Add model discovery and schema migration: discover only concrete user models inheriting `BaseModel`; create one standalone table per model in that company's selected database; reject models that do not inherit `BaseModel`.
- [ ] Provide typed, parameterized field-query specifications through `IDatabaseService<T>` for filtering, sorting, paging, and CRUD. Do not expose raw SQL, provider contexts, connections, commands, or `IQueryable` to user-owned services.
- [ ] Remove direct Postgres/SQLite package and project references from user-owned service templates. Add a build/analyzer gate rejecting `Npgsql`, `Microsoft.Data.Sqlite`, `DbConnection`, `DbCommand`, `FromSql`, `ExecuteSql*`, and raw SQL literals in user-owned service code.

## Acceptance tests

- [ ] Provision Acme and Beta from the root CompanyHQ directory.
- [ ] Verify one Postgres container is running and both `company_acme` and `company_beta` exist.
- [ ] Connect with `company_acme_app`: query Acme data successfully; connection to `company_beta` is denied.
- [ ] Connect with `company_beta_app`: query Beta data successfully; connection to `company_acme` is denied.
- [ ] Verify Postgres survives container recreation because `{server-root}/CompanyHQ/data/postgres/` persists.
- [ ] Verify no password or tenant data is present in the SQLite registry, repository, or generated Compose file.
- [ ] Verify the same `IDatabaseService<T>` CRUD and typed field-filter test suite passes against both Postgres and SQLite.
- [ ] Verify a `BaseModel`-derived model receives its own table, while a non-derived model does not.
- [ ] Verify a user service containing a raw SQL API, provider connection, or forbidden provider namespace fails the build gate.

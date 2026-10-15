# Postgres

Owns EF Core/Npgsql/PostGIS, the shared context/connection pool, model discovery, company-managed
migrations and the implementation of Persistence ports. Models/configurations remain feature-owned;
Postgres owns no feature rule. Normal feature persistence goes through Persistence, not this adapter.

## Rules

- Discover every concrete historic or neutral BaseModel subclass from the host dependency graph,
  including package consumers, then apply owning-assembly mappings. Runtime and design time must
  discover the same schema; never list feature models by hand or rewrite company migrations.
- Preserve snake-case tables/columns, UUIDv7 generation, timestamps, indexes, foreign keys and
  concurrency configuration. BaseModel's original name forwards to the neutral assembly; all
  repository constraints accept the original base, not only a newer sibling.
- Every scope shares one context across old repositories, new repositories and the unit of work.
  Old SDK-returning APIs remain an explicit compatibility/provider surface, never a neutral port.
- Open transactions inside the execution strategy, clear tracking before each attempt and commit
  only database work there. Preserve transient provider exceptions until the strategy handles them;
  translate data/concurrency failures at the neutral boundary.
- Tag-and-interceptor row locking is provider-owned and refuses use outside a transaction. Retain
  SKIP LOCKED semantics for leased queue claims; provider calls happen after claim commit.
- Exercise with disposable PostGIS; no deployed database or destructive reset in verification.
- Person/device precise location is never persisted. Explicit server-owned geography remains a
  provider-specific deployment capability, not a fake neutral query function.

## Decisions and limits

Postgres is the only concrete provider. Feature-owned EF configuration is intentionally retained
as deployment schema declarations (see Docs/provider-upgrade.md); no EF/Npgsql calls belong in
normal feature service paths. Shared public permission/audience/paging contracts keep their old
package names. This split is independent of the experimental cross-language DB service.

## Traps

Filter/order/count over entities and project last. Search only fields in the caller's DTO shape;
never widen an audience for convenience. SQL LIKE must use an explicit backslash escape to treat
user %, _ and backslash literally. Set-based updates need explicit timestamp setters and do not
refresh tracked rows. Nested transactions join the outer transaction, including its isolation and
retry. Seeders are idempotent and must save staged work before returning. Companies own their whole
migration set: HQ migrations are not a consumer's deployment migration set.

The catalog selection singleton is independent of CompanyProfile and its migration only adds a new
table. Generate schema from the full host graph even when features are runtime-disabled; do not drop
feature tables based on enablement. New named management permissions are seeded through the existing
role mechanism, without changing custom-role grants.

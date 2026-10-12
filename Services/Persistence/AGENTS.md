# Persistence

Owns provider-neutral in-process persistence capabilities, not a database, HTTP surface, domain model,
or cross-language transport. Postgres implements these ports; issue #2's experimental remote DB runtime
is separate and must not be made a feature dependency.

## Rules

- No EF, Npgsql, IQueryable, DbContext, provider configuration or provider project dependency here.
  Expressions describe bounded relational operations; they are not arbitrary provider escape hatches.
- Models belong to their features. The historic BaseModel namespace lives in this neutral assembly
  and Postgres forwards it: old and new models must satisfy the same repository constraints.
- All ports resolved in one scope share one unit of work. Repositories save immediately; staged
  multi-model changes save together. Snapshot reads and tracked reads must remain distinct.
- Transactions retry the whole delegate with clean tracking. Stage everything inside it, and send
  email, invalidate cache or call providers only after commit. Nested work joins the outer transaction.
- Explicit row locking is relational capability, not simulated independence for non-relational stores.
  Locks require a transaction; skip-locked reads retain ordering and bounds before materialization.
- Preserve timestamps, UUIDv7, schema and owning-assembly mappings. A package split is not a migration.

## Decisions

This is a closed relational query port, not a universal ORM. No implicit client evaluation, raw SQL,
provider function, or model discovery implementation belongs in it. Shared public paging/audience/
permission wire types retain their old Postgres.Contracts package for compatibility; that package
contains no provider runtime. Feature-owned EF mapping declarations are an explicit deployment-schema
exception, never normal feature persistence paths.

## Traps

An execution delegate may run repeatedly. Captured entities from a failed attempt are stale. Set-based
updates change only named fields (including UpdatedAt when intended); they do not refresh tracked
objects. Order through a unique tie-breaker before paging and project last. Provider exceptions must
be translated without hiding transient errors from the retry strategy.

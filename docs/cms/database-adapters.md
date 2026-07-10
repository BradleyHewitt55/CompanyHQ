# Database adapters and dynamic fields

> **Document status:** Target V1 architecture decision; PostgreSQL is confirmed.

CompanyHQ uses a hybrid relational/JSONB model. Stable operational concepts—users, roles, sessions, collection definitions, documents, revisions, workflows, approvals, tasks, runs, jobs, projects, audit, and outbox—use relational tables. Configurable document values live in a validated JSONB `data` column keyed by a registered schema revision.

This avoids unsafe runtime DDL for every field while retaining transactional workflow and revision relations. Queryable fields declare indexes in configuration. The database package manages expression/GIN indexes or typed projection tables through online migration jobs; unsupported filters fail configuration validation instead of degrading into unbounded scans.

All repositories require a `CompanyUnitOfWork` and automatically constrain `installation_id`. Optimistic concurrency uses an integer `version` plus immutable revision ID. Commands update with `WHERE id = ? AND version = ?`; zero rows produces a conflict, never last-write-wins.

Database adapters implement CompanyHQ-owned repository ports and capability metadata. PostgreSQL is the only V1 adapter. V2 adapters must pass the same transaction, isolation, versioning, query, migration, and security contract tests.

# Database and persistent data

> **Document status:** Target V1 architecture contract; superseded where more specific by `cms/database-adapters.md`.

CompanyHQ needs durable, policy-aware operational data. A relational system is the recommended initial source of truth because staff access, services, projects, tasks, approvals, commerce, audit, model attributes, and DTO versions are relational. Each company gets a separate database identity and credentials; separate databases are the preferred initial isolation boundary.

This is a data contract, not a requirement for a specific database product or ORM.

## Data ownership

| Data class | Owner |
| --- | --- |
| Company profile, staff, roles, sessions, service installation, projects, agent profiles, tasks, Changes, approvals, audits, notifications, and deployments | Installation-local application services. |
| Content, forms, SEO metadata, and domain records | Their owning core or optional service. |
| Model attribute/DTO registry and field policy | Owning service with CompanyHQ policy registry. |
| Asset metadata | CompanyHQ/media service; object bytes remain in object storage. |
| Source code, overrides, migrations, and project files | Git repositories, with safe operational references stored here. |
| Secrets | Secret storage; database stores only safe references and rotation/health metadata. |

No service reads/writes another service's internal tables directly without an explicit supported contract. Cross-service needs use versioned APIs, DTOs, events, or approved shared records.

## Required record families

The design supports at least:

- installation/company profile, environment, server composition, provider connection, and installation source repositories;
- staff identity, role, invitation, session/device, API/service key, MFA/recovery state, and delegated-agent capability;
- service installation, version, configuration reference, health, dependency, model, attribute, DTO, and API/MCP registration;
- project, repository, branch, worktree, preview, release, deployment, and rollback;
- content record, revision, lock, asset reference, and lifecycle state;
- task, Pipeline item, agent run, UiEditIntent, Change, approval, diff, checkpoint, test result, and notification;
- audit event, integration event, durable job, backup snapshot, restore drill, retention rule, and recovery evidence.

## Model attribute lifecycle

Adding a model attribute through Settings creates a source-backed Change. It declares field policy, DTO exposure, API version impact, migration, audit, validation, test, and rollback. No unreviewed schema or DTO mutation is allowed directly in production.

Model fields remain internal until a DTO version explicitly includes them and the active policy allows them. Deleting or renaming a field creates a new DTO/API major where the public contract changes; earlier published majors stay supported through an explicit compatibility projection. Migration and recovery planning are mandatory. See [models and DTOs](models-and-dtos.md).

## Tenant isolation

Every persisted record carries company ownership even when a separate database is the primary boundary. Queries, constraints, jobs, caches, object keys, audit reads, search, exports, DTO projection, and notification delivery enforce that ownership. A missing company context is an error, not permission to use a default tenant.

## Migrations and data changes

1. Declare model/service/DTO/API change and migration in a Change.
2. Review forward effect, data loss, compatibility, backup, downtime, field exposure, and rollback.
3. Validate in an isolated environment with required API/UI/security tests.
4. Approve and run alongside the matching deployment.
5. Record revision, outcome, recovery instructions, and test evidence.

Do not mix unreviewed production schema pushes with generated migrations. A migration failure stops promotion and enters recovery; it does not silently deploy an incompatible app.

## Revisions, locks, deletion, and backup

Editable content/configuration maintains revisions and actor attribution. Long-lived edits use locks/conflict workflow. Deletion defaults to retained/soft deletion where recovery is needed; permanent deletion requires retention policy, step-up/approval, audit, and backup consideration.

Database backups are encrypted, company-scoped, retained by policy, and restored regularly into isolated environments. A backup records source revision, service/model/DTO/API versions, migration level, media checkpoint, and restore result. It never contains plaintext third-party secrets.

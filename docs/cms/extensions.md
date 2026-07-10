# Extensions, hooks, and plugins

> **Document status:** Target V1 architecture contract.

Extensions use a versioned public SDK. They may contribute configuration transforms, fields, admin slots, endpoints, hooks, storage/provider adapters, agent tools, migrations, and health checks through declared capabilities. They cannot import private package paths or receive ambient database, filesystem, network, shell, Redis, or secret access.

Hook phases are `beforeValidate`, `afterValidate`, `beforeAuthorizeContext`, `beforeChange`, `afterChangeInTransaction`, `afterCommit`, `beforeReadProjection`, `afterReadProjection`, and delete/auth equivalents. Access is never a hook and cannot be disabled by one. Transactional hooks must be deterministic and bounded; external work belongs in outbox jobs.

Extension install/upgrade validates license, signature/provenance, compatible SDK range, permissions, routes/tools, migrations, rollback, and tests. Enabled extensions are installation-specific.

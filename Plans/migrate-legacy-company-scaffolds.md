# Migrate legacy company scaffolds

## Goal

Provide a safe migration from per-company Compose stacks and public ports to shared-server hosting.

## Scope

- Discover legacy company scaffolds, their editable content, configuration, and database data.
- Move editable content into the immutable company-slug directory.
- Convert each company from its own proxy, Postgres container, and ports to the shared services.
- Preserve rollback information until migration succeeds.

## Task-specific tests

- Migrate a legacy scaffold containing admin pages, projects, and database data.
- Assert the content and data are available from the new company directory and dedicated database.
- Assert the migrated Compose topology has no per-company proxy, Postgres service, or host ports.
- Simulate a migration failure and assert the legacy scaffold remains recoverable.

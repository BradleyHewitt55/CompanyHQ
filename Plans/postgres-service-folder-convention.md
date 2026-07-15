# Postgres service-folder convention

## Goal

Move Postgres injectable service classes into `Services/` without changing startup, model seeding, or database behavior.

## Scope

- Move and rename ServiceStateStore, PostgresService, ModelExtensionService, RelatedRecordsService, and DatabaseSchemaMigrator.
- Keep infrastructure classes in their current locations.
- Confirm the namespace filter in Postgres startup still excludes only Companies contract metadata.

## Task-specific tests

- Build Postgres and run its tests after the moves.
- Assert service-state DI resolves the renamed implementation.
- Start against a company database and verify entity-definition seeding remains unchanged.
- Grep for obsolete Postgres type names outside their replacement files.

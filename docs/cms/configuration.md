# CMS configuration

> **Document status:** Target V1 architecture contract.

CompanyHQ configuration is TypeScript data validated and compiled at startup/build time. `defineCompanyConfig` accepts collections, globals, fields, admin views, hooks, endpoints, extensions, storage/database adapters, workflows, and agent-tool contributions. It returns authoring types only; the compiler produces an immutable `RuntimeRegistry`.

Compilation order is fixed: load base config → apply enabled extension transforms in dependency order → normalize identifiers/defaults → validate schemas and route/component conflicts → validate access/workflow/tool declarations → build server registry → emit serializable admin manifest and generated TypeScript types → hash the configuration revision.

Plugins cannot mutate the registry after finalization. A transform receives a cloned configuration and an extension identity; output is revalidated. Duplicate slugs, routes, field paths, tool names, migrations, or admin slots fail startup. Server-only functions and secrets never enter the client manifest.

Schema changes are deployed as versioned configuration revisions with explicit PostgreSQL migrations/backfills where required. Adding a JSONB field normally needs no restart beyond deployment/config reload; changing indexed physical projections requires a migration job.

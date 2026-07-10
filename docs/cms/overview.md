# CMS overview

> **Document status:** Target V1 product contract.

The CompanyHQ CMS is the core content service used by the admin workspace and customer-facing projects. It owns content types, global settings, entries, drafts, revisions, publishing, localization, relationships, forms, SEO metadata, media references, access policy, hooks, and policy-projected DTOs.

## Core modules

| Module | Responsibility |
| --- | --- |
| Schema registry | Stable content-type, global, field, relationship, validation, localization, index, and ownership contracts. |
| Content repository | Transactional entry, draft, revision, publication, schedule, and restore operations. |
| Policy evaluator | Company, role, operation, record, field, locale, project, and delegation constraints. |
| DTO projector | Admin, client, preview, export, and agent views without raw-model exposure. |
| Query service | Bounded filter, sort, pagination, search, relationship depth, and locale resolution. |
| Workflow service | Draft, review, approval, publish, unpublish, scheduled work, and notifications. |
| Media service | Upload, validation, scan, metadata, variant, usage, delivery, retention, and deletion rules. |
| Forms service | Form definitions, validation, submissions, consent, abuse protection, notifications, and export. |
| SEO service | Canonical metadata, indexing controls, social cards, sitemap entries, and project rendering. |
| Event service | Transaction-safe audit events and durable work after commit. |

## Content boundaries

A content type defines repeatable entries. A global defines one company/project-scoped settings record. Both use stable keys, explicit ownership, field policy, DTO bindings, API majors, and migrations.

Models are internal. Every admin, project, API, MCP, export, notification, preview, and agent response uses a registered DTO. Relationship expansion repeats authorization and is bounded by depth and result limits.

## Operation flow

~~~text
authenticate -> resolve company/project and locale -> authorize operation
-> validate input and field policy -> execute transaction
-> write revision and audit record -> project response DTO
-> emit after-commit events -> run jobs and notifications
~~~

Create, read, update, delete, publish, unpublish, restore, duplicate, move, schedule, translate, export, and bulk operations each have separate policy. Permission for one never implies another.

## Admin flow

1. The user selects a registered content type or global.
2. List view returns a bounded admin-list DTO with filters allowed by field policy.
3. Detail view returns a separate admin-detail DTO plus revision and workflow controls permitted to the user.
4. Edits validate field access, schema, uniqueness, relationships, locale rules, and optimistic concurrency.
5. Saving creates a draft or published revision according to the content workflow.
6. Protected publication requires review, step-up, or approval where configured.
7. Project caches, search indexes, sitemap data, previews, and notifications update through idempotent jobs after commit.

## Project delivery

Projects request a client DTO for an explicit API major, locale, publication state, and preview context. Public calls receive published content only. Preview calls require a short-lived project- and revision-bound credential and never turn a draft into public content.

Delivery supports exact filters, stable cursor pagination, bounded relationship resolution, field selection limited to the DTO, caching keyed by company/project/API major/locale/policy/publication revision, and deterministic invalidation after publication.

## Schema changes

Adding or changing a content type, global, field, relationship, index, locale, validation rule, or DTO is a governed Change. It declares migration and backfill behavior, compatibility across every published API major, page bindings, policy, search and cache impact, rollback, backup, tests, and documentation.

The Settings UI can prepare such a Change but cannot mutate production schema directly. Destructive field changes require an explicit data migration and recovery proof.

## Required evidence

CMS changes test schema validation, record and field policy, tenant isolation, DTO omission, query limits, drafts and publication, revision restore, optimistic conflicts, localization, relationships, hook/job idempotency, cache invalidation, API-major compatibility, MCP mapping where exposed, backup/restore, and affected UI flows.

See [content lifecycle](content-lifecycle.md), [forms](forms.md), [SEO](seo.md), [media](media.md), [models and DTOs](../models-and-dtos.md), [hooks](hooks.md), and [testing](../testing.md).

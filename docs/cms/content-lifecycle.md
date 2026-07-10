# Content lifecycle

## Definitions

| Item | Meaning |
| --- | --- |
| Content type | Registered schema for repeatable entries. |
| Global | Registered singleton scoped to a company and optionally a project/locale. |
| Entry | Stable logical content identity. |
| Revision | Immutable snapshot created by a successful write. |
| Draft | Revision not available through public delivery. |
| Published revision | Exact revision currently visible to its approved audience. |
| Preview | Authorized rendering of a specific non-public revision. |

## Entry states

~~~text
draft -> in review -> scheduled or published -> superseded
  ^          |                 |                  |
  +----------+-----------------+---- restore -----+
published -> unpublished
~~~

Draft, In review, Scheduled, Published, Unpublished, and Archived are explicit states. A schedule records the exact revision, target time, timezone interpretation, actor, approval, and idempotency key. Editing after scheduling creates a new draft and does not silently alter the scheduled revision.

## Write functions

- Create validates create policy and writable fields, assigns a stable identifier, creates the first revision, and leaves publication to policy.
- Update requires the expected revision or equivalent concurrency value. Conflicts return a safe conflict DTO; last-write-wins is not the default.
- Publish selects an exact revision, validates publication policy and required fields, records the prior published revision, and emits an after-commit event.
- Unpublish removes public visibility without deleting history.
- Restore copies an authorized historical revision into a new draft. It never rewrites immutable history.
- Archive removes an entry from normal work lists while preserving policy, relationships, and retention behavior.
- Delete is soft by default. Permanent deletion requires dependency checks, retention policy, protected approval, audit, and recovery behavior.
- Bulk operations authorize and report each item; one permitted record never authorizes the rest.

## Revisions and comparison

Every revision records entry, schema version, locale, author, source operation, creation time, workflow state, safe change summary, and immutable field snapshot. Comparison applies the current viewer's field policy to both sides so old hidden data cannot leak through history.

Restore, duplicate, and localization copy operations create new revisions and preserve provenance. Audit records are separate from editable content history and cannot be rewritten by restoring content.

## Localization

The schema declares which fields are localized, required locales, fallback order, and publication independence. Locale fallback occurs only where the DTO and project allow it. A caller cannot request an unauthorized locale or infer hidden translations through counts, search, errors, or relationship expansion.

Changing the default locale or fallback rules is a migration-aware Change. Slugs may be locale-specific, but uniqueness, canonical URLs, and redirects must be defined per project.

## Relationships

Relationships declare target type, cardinality, required state, deletion behavior, allowed publication state, and DTO expansion. Writes validate that the target belongs to the same permitted company/project boundary. Reads reauthorize the target and omit or safely represent inaccessible relations.

Deletion behavior is one of restrict, clear, archive-dependent, or governed cascade. Unbounded cascade is forbidden. Relationship depth, breadth, and cycles are limited.

## Query functions

List and search operations accept only registered filter and sort fields, bounded page size, stable cursors, explicit locale, and a declared DTO. Full-text search indexes only fields allowed for that search surface. Counts and facets obey record policy and must not reveal inaccessible records.

Public queries return only published revisions. Admin queries may return additional states according to policy. Preview always binds an exact entry/revision/project and expires.

## Events

Successful writes emit versioned events after the transaction commits. Publication events can invalidate caches, update search and sitemap records, render previews, and notify users through idempotent jobs. A job reloads the exact revision and repeats its policy and service-state checks before external work.

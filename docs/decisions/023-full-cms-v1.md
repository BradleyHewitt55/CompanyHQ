# ADR-023: Full CMS scope in V1

> **Status:** Accepted. **Review:** at each roadmap release gate.

## Context and decision
V1 is not a thin agent shell. It includes config-driven collections/globals/fields, generated admin, auth/access, APIs, drafts, revisions, publishing, media, hooks/extensions, custom endpoints, tasks, agents, MCP, source control, audit, and operations.

## Alternatives considered
Headless CRUD MVP; agent orchestration first; admin-only prototype.

## Consequences, risks, security, and migration
The product is useful and architecturally coherent but scope is large. The 31-phase roadmap ships vertical foundations with release gates. Security boundaries land before agent mutations. Scope removal requires owner review, not silent deferral.

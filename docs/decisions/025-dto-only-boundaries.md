# ADR-025: DTO-only external boundaries

> **Status:** Accepted. **Review:** when any new transport/export is added or by 2027-01-15.

## Context and decision
Raw models leak fields and couple clients to storage. Every UI, API, MCP, agent, export, notification, cache, and webhook output uses a registered policy-projected DTO.

## Alternatives considered
Serialize ORM models; client-side field filtering; GraphQL field choice without server allowlists.

## Consequences, risks, security, and migration
Explicit contracts add work but make exposure reviewable and versionable. Projection omissions are safer than accidental inclusion. Any legacy raw response must be removed before public compatibility promises.

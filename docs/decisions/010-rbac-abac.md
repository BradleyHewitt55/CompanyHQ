# ADR-010: Central RBAC and ABAC

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Roles alone cannot express record, field, workflow, task, or approval constraints. Use owner/admin/developer/client RBAC grants narrowed by centrally evaluated ABAC.

## Alternatives considered
RBAC only; per-route checks; policy embedded in UI/config callbacks without a central evaluator.

## Advantages, disadvantages, and risks
Consistent constraints protect every transport. Policy complexity can become opaque; named policies, stable reason codes, matrices, and decision audit maintain clarity.

## Consequences and migration
UI visibility is derived, never authoritative. Internal jobs and tools reauthorize.

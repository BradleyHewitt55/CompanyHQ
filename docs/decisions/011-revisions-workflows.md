# ADR-011: Immutable revisions and workflows

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Agent and human edits need safe review and rollback. Create an immutable revision for every mutation and explicit draft/review/approval/publish state transitions.

## Alternatives considered
Mutable rows plus audit log; Git-only content history; snapshots only on publish.

## Advantages, disadvantages, and risks
History and rollback are reliable. Storage grows and sensitive history persists; retention, encryption, projection, and access on historical reads address this.

## Consequences and migration
Restore creates a new revision. Publishing advances a pointer only after current approval and version checks.

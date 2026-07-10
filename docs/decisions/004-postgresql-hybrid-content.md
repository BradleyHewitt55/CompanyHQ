# ADR-004: PostgreSQL relational and JSONB content

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Fields must be dynamically configured without unsafe DDL for every edit. Store operational entities relationally and validated document payloads in JSONB with declared expression/GIN indexes or projection tables.

## Alternatives considered
Table-per-collection dynamic DDL; EAV; document database; JSONB for everything.

## Advantages, disadvantages, and risks
Transactional workflows and flexible schemas coexist. Query/index complexity is the cost; configuration rejects unsupported/unindexed production queries and migrations manage physical projections.

## Consequences and migration
Every document records schema revision. Destructive schema changes require governed backfill/migration and rollback evidence.

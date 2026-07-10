# ADR-005: Application-service database boundary

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Authorization, versions, workflows, and audit fail if callers write directly. Only database adapters behind context-bound application services access PostgreSQL.

## Alternatives considered
ORM access from route handlers; agent SQL tools; service-specific direct repositories.

## Advantages, disadvantages, and risks
One mutation pipeline makes policy testable and auditable. It adds ceremony; command/query scaffolding and contract generators reduce repetition without bypasses.

## Consequences and migration
Architecture lint tests prohibit database imports from web, tools, agents, hooks, and extensions. Existing direct access, if later found, must be migrated before feature work continues.

# ADR-002: TypeScript pnpm monorepo

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
The CMS, Next.js UI, contracts, tools, workers, and CLI benefit from one type system. Use strict TypeScript, pnpm workspaces, explicit package export maps, and a task build graph.

## Alternatives considered
Multiple repositories; JavaScript; Python services; .NET services.

## Advantages, disadvantages, and risks
Shared Zod/types and atomic refactors improve consistency. Monorepo coupling and slow builds are risks; import rules, package contracts, caching, and ownership contain them.

## Consequences and migration
No other language enters V1 without an ADR proving a technical need. The first scaffold establishes package boundaries before features.

# ADR-008: Compiled CMS configuration

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Collections, fields, routes, admin, tools, and extensions need one truth. Compile TypeScript configuration through ordered transforms and validation into immutable server/admin registries and generated types.

## Alternatives considered
Convention-only file routing; mutable runtime registration; database-only schema builder.

## Advantages, disadvantages, and risks
Conflict detection and shared behavior improve DX and safety. Startup/build complexity rises; deterministic compiler fixtures and configuration hashes control it.

## Consequences and migration
Server functions never enter client manifests. Registry mutation after finalization is prohibited.

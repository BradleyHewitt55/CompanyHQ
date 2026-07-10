# ADR-020: Versioned public extension SDK

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Custom fields, views, hooks, endpoints, adapters, and tools need stable boundaries. Extensions use a capability-declared, versioned public SDK and config transforms; private imports are forbidden.

## Alternatives considered
Forking core; arbitrary in-process modules; no extensions.

## Advantages, disadvantages, and risks
Extensibility remains upgradeable. In-process plugins are supply-chain and availability risks; provenance, compatibility, permission review, timeouts, and future stronger isolation mitigate them.

## Consequences and migration
Extensions cannot bypass context/policy/mutation/audit. Breaking SDK changes require a new major and migration guide.

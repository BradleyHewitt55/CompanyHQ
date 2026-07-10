# ADR-009: Registry-driven admin and sparse overrides

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Admin screens must be generated yet customizable without exposing/forking supplied source. Generate routes/navigation/forms from the registry and allow sparse public-slot overrides.

## Alternatives considered
Full admin fork; fixed screens; arbitrary filesystem route discovery.

## Advantages, disadvantages, and risks
Upgrades remain viable while companies customize. Slot contract drift is the risk; versioned props, compatibility checks, fallback, and deployment tests control it.

## Consequences and migration
Overrides cannot replace auth, policy, mutation, tenant, or audit boundaries.

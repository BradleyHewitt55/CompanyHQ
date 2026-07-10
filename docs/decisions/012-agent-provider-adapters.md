# ADR-012: Provider-neutral agent adapters

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Codex, Claude, Pi, and Hermes differ in sessions/events/tools. Normalize them behind CompanyHQ-owned lifecycle, event, approval, usage, cancellation, and recovery contracts.

## Alternatives considered
Provider-specific product flows; one embedded agent runtime; direct SDK use in UI/workers.

## Advantages, disadvantages, and risks
Provider choice and failure isolation improve. Lowest-common-denominator design is a risk; capability negotiation and adapter-specific metadata preserve useful differences.

## Consequences and migration
CompanyHQ task/run state is authoritative. Provider IDs are opaque adapter data.

# ADR-006: Central company context

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Manual company filters are error-prone. Resolve installation context from trusted server/job/capability configuration and bind repositories, caches, queues, files, events, and audit to it.

## Alternatives considered
Caller-supplied company IDs; developer-added filters; database RLS alone.

## Advantages, disadvantages, and risks
Central enforcement prevents omission. Context propagation mistakes remain possible; signed envelopes, typed APIs, RLS defense in depth, and cross-installation tests address them.

## Consequences and migration
No repository or tool accepts arbitrary company selection. V1 has no application super-admin.

# Health checks

> **Document status:** Target V1 operational contract.

Liveness reports process responsiveness only. Readiness validates configuration, database migration/connectivity, Redis/BullMQ, required storage, audit, key material, and role-specific dependencies. Deep authenticated diagnostics test a safe end-to-end job and storage round trip.

Provider, source-control, email, and optional integrations report independent degraded states; optional failure should not make public CMS reads unavailable. Health responses expose no versions/hosts/secrets to unauthenticated callers beyond coarse status.

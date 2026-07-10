# Secrets

> **Document status:** Target V1 security contract.

Secrets are stored in protected installation files or a pluggable secret manager, encrypted at rest, referenced by opaque IDs, and injected only into the adapter that needs them. `.env` is never committed, mounted into agent containers, returned by APIs/MCP, copied to jobs, or logged.

Each secret records owner, purpose, scope, provider, created/rotated/last-used times, and revocation state without the value. Rotation supports overlapping key IDs, session/capability invalidation, and audited verification. Production startup rejects default or missing critical secrets.

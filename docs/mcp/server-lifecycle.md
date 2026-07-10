# MCP server lifecycle

> **Document status:** Target V1 operational contract.

Startup validates installation identity, keys, registry/config revision, application-service health, policy, and audit sink before readiness. Draining rejects new sessions, cancels or allows bounded completion, persists final audit, and revokes session capabilities. Key/config changes trigger graceful rotation, not an authorization gap.

Health distinguishes liveness, readiness, registry revision, downstream health, active/draining sessions, and degraded audit state. Mutation tools are unavailable when durable audit or application services are unhealthy.

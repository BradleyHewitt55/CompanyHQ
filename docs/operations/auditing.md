# Auditing

> **Document status:** Target V1 architecture contract.

Audit events are append-only PostgreSQL records with installation, actor type/ID, user/delegator, agent/profile/connection, session, task/run/tool, action, resource/type/version, policy/config revision, decision, timestamp, correlation/causation/idempotency, source IP/client, and safe before/after references.

Security/business audit is distinct from debug logs and cannot be disabled by callers. A hash chain or signed export detects tampering. Retention, export, legal hold, and access are policy controlled. Mutations fail closed when their required audit record cannot commit atomically.

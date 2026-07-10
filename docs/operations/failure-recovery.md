# Failure recovery

> **Document status:** Target V1 operational contract.

Recovery favors reconciliation over blind retry. PostgreSQL tasks/runs/schedules/outbox and immutable revisions are authoritative; Redis is rebuilt from them. Startup reconcilers detect orphaned leases, stuck outbox rows, missing repeatable jobs, provider sessions, pending approvals, and worktrees.

Runbooks cover database/Redis/storage/provider/source host loss, queue backlog/DLQ, compromised credentials, failed migration, audit outage, disk pressure, and restore. Every destructive step requires explicit human confirmation and captured recovery point.

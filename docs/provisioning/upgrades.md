# Upgrades

> **Document status:** Target V1 operational contract.

Upgrade preflight checks supported current/target versions, disk/database capacity, configuration compatibility, extension SDK ranges, migration plan, healthy queues, active agent runs, and a verified backup. The release is identified by immutable image digests.

Workers stop claiming incompatible jobs, active leases drain or pause, migrations run once under a lock, services restart, and readiness/contract smoke tests execute. Rollback is allowed only when migrations are backward compatible; otherwise restore is required. Every stage and operator decision is audited.

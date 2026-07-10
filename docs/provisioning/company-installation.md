# Company installation

> **Document status:** Target V1 implementation contract.

The installer creates the canonical [server layout](../architecture/server-layout.md), a unique Compose project/network, database/user, Redis prefix and credentials, application secret keys, storage scope, queue namespaces, MCP audience, and backup identity. It pins an application release and configuration revision.

Owner creation happens through the same auth application service used at runtime, in a one-use bootstrap mode bound to the installation journal. Bootstrap authority expires after successful health checks and cannot create a second owner.

Completion requires web/admin readiness, database migration status, Redis/BullMQ round trip, storage write/read/delete probe, scheduler leadership, worker heartbeat, audit append, backup configuration, and owner login verification.

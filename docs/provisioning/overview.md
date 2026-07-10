# Provisioning overview

> **Document status:** Confirmed Target V1 contract.

Provisioning stages are connect → verify host → preflight → interview → create installation journal/root → install/reconcile application release → generate secrets/configuration → start isolated Compose project → migrate → create owner → health check → handoff.

Every stage is idempotent and journaled. Retry resumes after inspecting actual server state. The installer never deletes an existing directory, database, volume, repository, object prefix, or backup during automatic recovery.

The company interview records name/key, description, target market, timezone, domain/TLS choice, storage and backup targets, source-control settings, initial owner, and enabled optional providers. Secrets are written through protected remote channels and never copied into documentation or logs.

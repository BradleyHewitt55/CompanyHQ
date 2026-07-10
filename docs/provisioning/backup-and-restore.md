# Backup and restore

> **Document status:** Target V1 operational contract.

Backups cover PostgreSQL, installation-owned object/media data, encrypted configuration/secrets references, repositories not already protected remotely, extension source, and the release/configuration manifest. Redis queues are not a business-state backup; durable task/run/schedule/outbox state in PostgreSQL enables reconciliation.

Backups are encrypted, checksummed, retention-managed, access-audited, and stored outside the installation host. Restore targets an empty/quarantined installation, validates installation identity, migrates if supported, reconciles storage and queues, rotates secrets where exposure is possible, and runs cross-boundary plus content/version integrity checks before traffic resumes.

Restore drills are mandatory release evidence. A backup that has never been restored is a wish, not a recovery plan.

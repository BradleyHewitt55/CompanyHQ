# Development and production environments

> **Document status:** Target V1 recommendation.

Development uses the same service boundaries and Compose topology with disposable local credentials, local object storage, test model providers, and deterministic job clocks. Production uses pinned images, TLS, externalized backups, protected secrets, restricted egress, resource limits, and telemetry export.

Configuration is validated at startup and includes an explicit environment. Production refuses development auth, wildcard origins, debug errors, unencrypted provider credentials, unrestricted agent modes, missing backup targets, or unversioned migrations. Seed/demo data is never loaded into production.

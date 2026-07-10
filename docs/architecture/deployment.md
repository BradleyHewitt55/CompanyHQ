# Deployment architecture

> **Document status:** Target V1 recommendation.

Each installation uses its own Docker Compose project name, network, volumes, secrets, health checks, and resource controls. The minimum services are web, worker, scheduler, PostgreSQL, Redis, and optional reverse-proxy/storage adapters. Agent provider workers are added only when configured.

Deployments use immutable application image versions and a migration gate: backup/restore preflight → compatible schema migration → health/readiness checks → traffic switch → observation window. A failed release rolls application images back only when the schema remains compatible; otherwise the documented restore procedure is required.

Agents cannot invoke deployment operations. Owners or authorized admins approve a release whose exact image digests, migrations, configuration revision, test evidence, and backup reference are recorded.
